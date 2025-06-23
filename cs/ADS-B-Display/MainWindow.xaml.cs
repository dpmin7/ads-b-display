using ADS_B_Display.Map.MapSrc;
using ADS_B_Display.Models;
using ADS_B_Display.Views.Popup;
using Microsoft.Win32;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
//using System.Windows.Shapes;
using System.Windows.Threading;

namespace ADS_B_Display
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private struct TrackHookStruct
        {
            public bool Valid_CC;
            public uint ICAO_CC;
            public bool Valid_CPA;
            public uint ICAO_CPA;
            public Dictionary<string, string> DepartureAirport;
            public Dictionary<string, string> ArrivalAirport;
        }

        private class ADSBAircraft
        {
            public uint ICAO;
            public bool HaveLatLon;
            public double Latitude;
            public double Longitude;
        }

        private const float BG_INTENSITY = 0.37f; // 배경색 강도 (0.0f ~ 1.0f)
        private const float MAP_CENTER_LAT = 40.73612f; // 지도 중심 위도
        private const float MAP_CENTER_LON = -80.33158f; // 지도 중심 경도

        // ─── 1) Raw Connect 관련 필드 ───
        private bool _isRawConnected = false;

        // ─── 2) SBS Connect 관련 필드 ───
        private bool _isSbsConnected = false;

        // ─── 3) SBS Record 관련 필드 ───
        private bool _isRecordingSbs = false;

        // ─── 4) Raw Record 관련 필드 ───
        private bool _isRecordingRaw = false;

#if false // PingEcho 테스트용 코드 (필요시 활성화)    
        // Ping 관련 필드
        private PingEcho pingEcho = new PingEcho();
#endif

        // Map 관련 필드
        double Mw1, Mw2, Mh1, Mh2, xf, yf;
        public Vector3d[] Map_v = new Vector3d[4];
        public Vector3d[] Map_p = new Vector3d[4];
        public Vector2d[] Map_w = new Vector2d[2];
        double MapCenterLat, MapCenterLon;
        private bool _loadMapFromInternet = true;

        public ObservableCollection<AircraftForUI> Aircrafts { get; set; } = new ObservableCollection<AircraftForUI>();
        private List<uint> updated = new List<uint>();
        private DispatcherTimer _updateTimer = new DispatcherTimer();

        private TrackHookStruct _trackHook = new TrackHookStruct();
        private SbsWorker _sbsWorker = null;
        private SbsWorker _rawWorker = null;
        public MainWindow()
        {
            InitializeComponent();
            var settings = new GLWpfControlSettings() {
                MajorVersion = 2, // OpenGL Major Version
                MinorVersion = 1, // OpenGL Minor Version
            };
            glControl.Start(settings);

            //InitializeBackend();
            MapCenterLat = MAP_CENTER_LAT;
            MapCenterLon = MAP_CENTER_LON;

            LoadMap(TileServerType.GoogleMaps);
            SetMapCenter(out double x, out double y);
            _earthView.Eye.X = x;
            _earthView.Eye.Y = y;
            _earthView.Eye.H /= Math.Pow(1.3, 18); // 높이(줌)도 필요시 조정
            //dg.ItemsSource = Aircrafts;

            _sbsWorker = new SbsWorker(OnSbsMessageReceived);
            _rawWorker = new SbsWorker(OnRawMessageReceived);

            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += _updateTimer_Tick;
            _updateTimer.Start();

            // read aircraft data from file if exists
            var aircraftDir = $"{Directory.GetCurrentDirectory()}\\AircraftDB";
            //AircraftDB.Init(aircraftDir);
        }

        private async void _updateTimer_Tick(object sender, EventArgs e)
        {
            glControl.InvalidateVisual(); // OpenGL 컨트롤 강제 갱신

            List<uint> temp;
            lock (lockObj) {
                temp = updated.ToList();
                updated.Clear();
            }
            foreach (var icao in temp) {
                var aircraft = AircraftManager.GetOrAdd(icao);
                var airUI = Aircrafts.FirstOrDefault(a => a.ICAO == aircraft.ICAO);
                if (airUI == null) {
                    airUI = new AircraftForUI();
                    Aircrafts.Add(airUI);
                }
                await Task.Run(() => {
                    airUI.UpdateAircraftForUI(aircraft);
                });
            }

            SystemTimeText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void Purge_Click(object sender, RoutedEventArgs e)
        {
            // Purge 버튼 클릭 시 로직
        }

        private void RawRecordButton_Click(object sender, RoutedEventArgs e)
        {
            // 1) 이미 기록 중이면 중단하고 리소스 해제
            if (_isRecordingRaw) {
                try {
                    _rawWorker.RecordOff();
                } catch (Exception ex) {
                    MessageBox.Show($"Error occur while Raw record file is closing.:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                _isRecordingRaw = false;
                RawRecordButton.Content = "Raw Record";  // 버튼 텍스트 원복
                MessageBox.Show("Raw record is stopped.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2) 기록 중이 아니면, SaveFileDialog 띄워서 파일 경로 선택
            var dlg = new SaveFileDialog {
                Title = "Raw Record file save",
                Filter = "Raw Log (*.raw)|*.raw|모든 파일 (*.*)|*.*",
                FileName = $"RawLog_{DateTime.Now:yyyyMMdd_HHmmss}.raw", // 기본 파일명 예시
                DefaultExt = ".raw"
            };

            bool? result = dlg.ShowDialog();
            if (result != true) {
                // 사용자가 취소했으면 아무 동작 없이 종료
                return;
            }

            string path = dlg.FileName;
            try {
                _rawWorker.RecordOn(path);
            } catch (Exception ex) {
                MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _rawWorker.RecordOff();
                return;
            }

            // 3) 기록 플래그 켜고 버튼 텍스트 변경
            _isRecordingRaw = true;
            RawRecordButton.Content = "Stop RAW Record";

            // 4) 녹화를 시작했다는 안내 메시지 (선택 사항)
            MessageBox.Show($"Start Raw record:\n{path}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void StopRawPlaybackButton_Click(object sender, RoutedEventArgs e)
        {
            // Raw 플레이백 중지 로직
        }

        private void UseSbsRemote_Click(object sender, RoutedEventArgs e)
        {
            // “ADS-B Hub” 메뉴 클릭 시 로직
            SbsConnectTextBox.Text = "data.adsbhub.org";
        }

        private void CloseControl_Click(object sender, RoutedEventArgs e)
        {
            // “Close Control” 클릭 시 로직 (선택 해제 등)
        }

        private void TimeToGoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 예: 초 단위로 e.NewValue를 받아와 “hh:mm:ss.fff” 형태로 변환 후 TimeToGoValueText.Text에 대입
            TimeSpan t = TimeSpan.FromSeconds(e.NewValue);
            TimeToGoValueText.Text = string.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                                                   t.Hours,
                                                   t.Minutes,
                                                   t.Seconds,
                                                   t.Milliseconds);
        }
        
        private void SbsRecordButton_Click(object sender, RoutedEventArgs e)
        {
            // 1) 이미 기록 중이면 중단하고 리소스 해제
            if (_isRecordingSbs) {
                try {
                    _sbsWorker.RecordOff();
                } catch (Exception ex) {
                    MessageBox.Show($"SBS 기록 파일을 닫는 동안 오류가 발생했습니다:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                _isRecordingSbs = false;
                SbsRecordButton.Content = "SBS Record";  // 버튼 텍스트 원복
                MessageBox.Show("SBS record is stopped.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2) 기록 중이 아니면, SaveFileDialog 띄워서 파일 경로 선택
            var dlg = new SaveFileDialog {
                Title = "SBS Record file save",
                Filter = "SBS Log (*.sbs)|*.sbs|모든 파일 (*.*)|*.*",
                FileName = $"SbsLog_{DateTime.Now:yyyyMMdd_HHmmss}.sbs", // 기본 파일명 예시
                DefaultExt = ".sbs"
            };

            bool? result = dlg.ShowDialog();
            if (result != true) {
                // 사용자가 취소했으면 아무 동작 없이 종료
                return;
            }

            string path = dlg.FileName;
            try {
                _sbsWorker.RecordOn(path);
            } catch (Exception ex) {
                MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _sbsWorker.RecordOff();
                return;
            }

            // 3) 기록 플래그 켜고 버튼 텍스트 변경
            _isRecordingSbs = true;
            SbsRecordButton.Content = "Stop SBS Record";

            // 4) 녹화를 시작했다는 안내 메시지 (선택 사항)
            MessageBox.Show($"Start SBS record:\n{path}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SbsPlaybackButton_Click(object sender, RoutedEventArgs e)
        {
            if (SbsPlaybackButton.Content.ToString() == "SBS Playback" && sender != null) {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                if (dialog.ShowDialog() == true) {
                    string fileName = dialog.FileName;
                    if (!File.Exists(fileName)) {
                        MessageBox.Show("File " + fileName + " does not exist");
                    } else {
                        try {
                            _sbsWorker.Start(fileName);

                            SbsPlaybackButton.Content = "Stop SBS Playback";
                            SbsConnectButton.IsEnabled = false;
                        } catch (Exception ex) {
                            MessageBox.Show("Cannot open file " + fileName + "\n" + ex.Message);
                        }
                    }
                }
            } else {
                _sbsWorker.Stop();

                SbsPlaybackButton.Content = "SBS Playback";
                SbsConnectButton.IsEnabled = true;
            }
        }

        // Playback 속도
        private void PlaybackSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PlaybackSpeedValueText.Text = "PlayBack Speed: " + e.NewValue + "x";
            if (_sbsWorker != null)
            {
                _sbsWorker.setPlayBackSpeed((int)e.NewValue);
            }
        }
        /// <summary>
        /// Raw Connect 버튼 클릭 시 호출.
        /// 연결/해제를 토글(toggle) 방식으로 처리.
        /// RawConnectTextBox에 입력된 주소(예: "127.0.0.1:30002") 형태로 파싱해서 TcpClient 연결.
        /// </summary>
        private async void RawConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // 이미 연결 중이면 연결 해제
            if (_isRawConnected) {
                try {
                    _rawWorker.Stop();
                } catch { }
                _isRawConnected = false;
                RawConnectButton.Content = "Connect";
                MessageBox.Show("Raw feed 연결을 해제했습니다.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 연결 중이 아니면 TextBox에 입력된 host:port로 연결 시도
            string input = RawConnectTextBox.Text.Trim();
            if (string.IsNullOrEmpty(input)) {
                MessageBox.Show("Raw Connect 주소를 입력하세요. (예: 127.0.0.1:30002)", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // host와 port 분리
            string host;
            int port;
            if (input.Contains(":")) {
                var parts = input.Split(new[] { ':' }, 2);
                host = parts[0];
                if (!int.TryParse(parts[1], out port)) {
                    MessageBox.Show("포트 번호가 올바르지 않습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            } else {
                // 포트 미입력 시 기본값 사용 (예: 30002)
                host = input;
                port = 30002;
            }

            // 비동기로 TCP 연결 시도
            try {
                CancellationTokenSource cts = new CancellationTokenSource();
                var popup = new LoadingPopup() { WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this };
                popup.Closed += (s, e2) => { if (popup.IsCancelled) cts.Cancel(); };
                popup.Show();
                var res = await _rawWorker.Start(host, port, cts.Token);
                if (res) {
                    popup.Close();
                    _isRawConnected = true;
                    RawConnectButton.Content = "Disconnect";
                } else {

                }

            } catch (Exception ex) {
                MessageBox.Show($"Raw feed 연결 중 오류가 발생했습니다:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _rawWorker.Stop();
                _isRawConnected = false;
                RawConnectButton.Content = "Connect";
            }
        }

        

        /// <summary>
        /// SBS Connect 버튼 클릭 시 호출.
        /// 연결/해제를 토글(toggle) 방식으로 처리.
        /// SbsConnectTextBox에 입력된 주소(예: "data.adsbhub.org:30003") 형태로 파싱해서 TcpClient 연결.
        /// </summary>
        private async void SbsConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // 이미 연결 중이면 연결 해제
            if (_isSbsConnected) {
                try {
                    _sbsWorker.Stop();
                } catch { }
                _isSbsConnected = false;
                SbsConnectButton.Content = "Connect";
                MessageBox.Show("SBS Hub 연결을 해제했습니다.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
#if false // PingEcho 테스트용 코드 (필요시 활성화)           
                pingEcho.Stop();
#endif
                return;
            }

            // 연결 중이 아니면 TextBox에 입력된 host:port로 연결 시도
            string input = SbsConnectTextBox.Text.Trim();
            if (string.IsNullOrEmpty(input)) {
                MessageBox.Show("SBS Connect 주소를 입력하세요. (예: data.adsbhub.org:30003)", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // host와 port 분리
            string host;
            int port;
            if (input.Contains(":")) {
                var parts = input.Split(new[] { ':' }, 2);
                host = parts[0];
                if (!int.TryParse(parts[1], out port)) {
                    MessageBox.Show("포트 번호가 올바르지 않습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            } else {
                // 포트 미입력 시 기본값 사용 (예: 30003)
                host = input;
                port = 5002;
            }
            
            try {
                // 연결 후, 스트림에서 한 줄씩 읽어서 처리 (예시: OnSbsMessageReceived(rawLine))
                CancellationTokenSource cts = new CancellationTokenSource();
                var popup = new LoadingPopup() { WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this };
                popup.Closed += (s, e2) => { if (popup.IsCancelled) cts.Cancel(); };
                popup.Show();
                var res = await _sbsWorker.Start(host, port, cts.Token);
                if (res) {
                    popup.Close();
                    _isSbsConnected = true;
                    SbsConnectButton.Content = "Disconnect";

#if false // PingEcho 테스트용 코드 (필요시 활성화)
                    Console.WriteLine($"Ping 시작");

                    // Ping Echo 시작
                    pingEcho.Start(host, port, 2000, (pingHost, ex) => // 'host' 이름을 'pingHost'로 변경하여 충돌 방지
                    {
                        if (ex != null)
                        {
                            Console.WriteLine($"Ping 예외 발생: {pingHost} - {ex.Message}");
                        }
                        else
                        {
                            Console.WriteLine($"Ping 실패: {pingHost}");
                        }
                    });
#endif
                }
                else {
                    
                }
            } catch (Exception ex) {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error occur while SBS Hub is connecting.:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _isSbsConnected = false;
                    SbsConnectButton.Content = "Connect";
                });
            }
        }


        private object lockObj = new object();
        private FileSystemStorage _storage;
        private KeyholeConnection _keyhole;
        private TileManager _tileManager;
        private MasterLayer _masterLayer;
        private FlatEarthView _earthView;

        /// <summary>
        /// Raw feed에서 들어오는 한 줄짜리 메시지를 처리하는 메서드(예시).
        /// </summary>
        private uint OnRawMessageReceived(string rawLine)
        {
            var icao = AircraftManager.ReceiveRawMessage(rawLine);
            lock (lockObj) {
                updated.Add(icao);
            }

            return icao;
        }

        /// <summary>
        /// SBS 허브에서 들어오는 한 줄짜리 메시지를 처리하는 메서드(예시).
        /// 여기서는 “SBS Record” 기능과 연동하도록 구현.
        /// </summary>
        private uint OnSbsMessageReceived(string rawLine)
        {
            var icao = AircraftManager.ReceiveSBSMessage(rawLine);
            lock (lockObj) {
                updated.Add(icao);
            }

            return icao;
        }


        private void UseSbsLocal_Click(object sender, RoutedEventArgs e)
        {
            SbsConnectTextBox.Text = "128.237.96.41";
        }

        private void LoadArtccBoundaries_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {

        }

        private void PurgeStaleCheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void PurgeStaleCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {

        }

        private void CycleImagesCheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void CycleImagesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {

        }

        private void InsertAreaButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CompleteAreaButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CancelAreaButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DeleteAreaButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TimeToGoCheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void TimeToGoCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {

        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {

        }


        private void SetMapCenter(out double x, out double y)
        {
            double siny;
            x = (MapCenterLon + 0.0) / 360.0;
            siny = Math.Sin((MapCenterLat * Math.PI) / 180.0);
            siny = Math.Min(Math.Max(siny, -0.9999), 0.9999);
            y = (Math.Log((1 + siny) / (1 - siny)) / (4 * Math.PI));
        }

        private int _numSpriteImages;
        private void glControl_Loaded(object sender, RoutedEventArgs e)
        {
            glControl.RenderContinuously = false;
            glControl.InvalidateVisual(); // 첫 프레임 수동 렌더링

            // 뷰포트 설정: 컨트롤 크기에 맞춰 화면 전체를 사용
            GL.Viewport(0, 0, glControl.ActualWidth > 0 ? (int)glControl.ActualWidth : 1, glControl.ActualHeight > 0 ? (int)glControl.ActualHeight : 1);

            // 깊이 테스트 비활성화 (2D 렌더링)
            GL.Disable(EnableCap.DepthTest);

            // 알파 블렌딩 활성화
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // 선 생략(stipple) 기능 활성화
            GL.Enable(EnableCap.LineStipple);
            // GL.LineStipple(1, 0xAAAA);  // 필요시 패턴 지정

            // 비행기 스프라이트 이미지 로드/생성
            _numSpriteImages = Ntds2d.MakeAirplaneImages();
            Ntds2d.MakeAirTrackFriend();
            Ntds2d.MakeAirTrackHostile();
            Ntds2d.MakeAirTrackUnknown();

            // 선 후크 및 포인트 그리기 기하 생성
            Ntds2d.MakePoint();
            Ntds2d.MakeTrackHook();

            // 뷰 크기에 맞춰 카메라/뷰 재설정
            _earthView.Resize((int)glControl.ActualWidth, (int)glControl.ActualHeight);

            // 현재 선 속성 저장 후 복원 (필요시)
            GL.PushAttrib(AttribMask.LineBit);
            GL.PopAttrib();

            // 콘솔에 OpenGL 버전 출력
            Console.WriteLine($"OpenGL Version: {GL.GetString(StringName.Version)}");
            _isLoaded = true;
        }

        private double _x = 1.0f, _y = 0.5f, _z = 0.0f;

        private void glControl_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 마우스 왼쪽 버튼이 떼어졌을 때만 처리
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) {
                _MouseDownMask &= ~LEFT_MOUSE_DOWN;
            }
        }

        private double airplaneScale;
        private void Window_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                _earthView.SingleMovement(EarthView.NAV_ZOOM_IN);
            else _earthView.SingleMovement(EarthView.NAV_ZOOM_OUT);
            airplaneScale = (double)Math.Min((0.05 / _earthView.Eye.H), 1.5); // 스케일 계산

            UpdateRegion(); // 현재 지역 업데이트
            glControl.InvalidateVisual(); // 마우스 휠 이벤트 후 강제 갱신
        }

        private void glControl_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 마우스 위치 구하기
            var pos = e.GetPosition(glControl);
            int x = (int)pos.X;
            int y = (int)pos.Y;

            // 화면 좌표계 변환 (Y축 뒤집기)
            int X1 = x;
            int Y1 = (int)(glControl.ActualHeight - 1) - y;

            // Map_v, Map_w, xf, yf 등은 클래스 필드로 선언되어 있다고 가정
            if (Map_v != null && Map_w != null &&
                X1 >= Map_v[0].X && X1 <= Map_v[1].X &&
                Y1 >= Map_v[0].Y && Y1 <= Map_v[3].Y) {
                // 위도/경도 계산
                double VLat = Math.Atan(Math.Sinh(Math.PI * (2 * (Map_w[1].Y - (yf * (Map_v[3].Y - Y1)))))) * (180.0 / Math.PI);
                double VLon = (Map_w[1].X - (xf * (Map_v[1].X - X1))) * 360.0;

                // 위도/경도 표시 (예: Label 컨트롤 사용)
                LatText.Text = DMS.DegreesMinutesSecondsLat(VLat);
                LonText.Text = DMS.DegreesMinutesSecondsLon(VLon);

                // PointInPolygon 등 영역 판정 (Areas 컬렉션 필요)
                //double[] point = new double[3] { VLon, VLat, 0.0 };
                //if (Areas != null) {
                //    foreach (var area in Areas) {
                //        if (PointInPolygon(area.Points, area.NumPoints, point)) {
                //            // 예: 메시지 로그에 출력
                //            // MsgLog.Items.Add("In Polygon " + area.Name);
                //        }
                //    }
                //}
            }

            // 드래그 중이면 EarthView에 Drag 이벤트 전달
            if ((_MouseDownMask & LEFT_MOUSE_DOWN) != 0) {
                UpdateRegion(); // 현재 지역 업데이트
                _earthView.Drag(_MouseLeftDownX, _MouseLeftDownY, x, y, EarthView.NAV_DRAG_PAN);
                glControl.InvalidateVisual(); // 화면 갱신
            }

        }

        private void glControl_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 마우스 좌표 구하기 (정수형으로 변환)
            int x = (int)e.GetPosition(glControl).X;
            int y = (int)e.GetPosition(glControl).Y;
            // 마우스 왼쪽 버튼 클릭 확인
            if (e.ChangedButton == MouseButton.Left) {
                // Ctrl 키가 눌렸는지 확인
                if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0) {
                    // Ctrl + 왼쪽 클릭 시 동작 (필요시 구현)
                } else {
                    

                    // 필요한 전역 변수에 값 저장 (예시)
                    _MouseLeftDownX = x;
                    _MouseLeftDownY = y;
                    _MouseDownMask |= LEFT_MOUSE_DOWN;

                    // EarthView의 StartDrag 호출
                    _earthView.StartDrag(x, y, EarthView.NAV_DRAG_PAN);
                }
            } else if (e.ChangedButton == MouseButton.Right) {
                //if (AreaTemp != null) {
                //    if (AreaTemp.NumPoints < Area.MaxPoints) {
                //        AddPoint(e.X, e.Y);
                //    } else {
                //        MessageBox.Show("Max Area Points Reached");
                //    }
                //} else {
                    bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
                    HookTrack(x, y, ctrl);
                //}
            } else if (e.ChangedButton == MouseButton.Middle) {
                //ResetXYOffset();
            }
        }

        private void HookTrack(int x, int y, bool cpaHook)
        {
            // 화면 좌표를 위도/경도로 변환
            XYtoLatLon(x, glControl.ActualHeight - y, out double vLat, out double vLon);

            double minRange = 16.0;
            uint currentICAO = 0;

            foreach (var data in AircraftManager.GetAll())
            {
                if (data.HaveLatLon)
                {
                    double dLat = vLat - data.Latitude;
                    double dLon = vLon - data.Longitude;
                    double range = Math.Sqrt(dLat * dLat + dLon * dLon);
                    if (range < minRange)
                    {
                        currentICAO = data.ICAO;
                        minRange = range;
                    }
                }
            }

            if (minRange < 0.2)
            {
                var selectedAircraft = AircraftManager.GetOrAdd(currentICAO);
                if (!cpaHook)
                {
                    _trackHook.Valid_CC = true;
                    _trackHook.ICAO_CC = selectedAircraft.ICAO;
                    Console.WriteLine(AircraftDB.GetAircraftInfo(selectedAircraft.ICAO));

                    // 출발 - 도착 정보 저장
                    List<Dictionary<string, string>> airportsInfo = AirportDB.GetAirPortsInfo();
                    List<Dictionary<string, string>> routesInfo = AirportDB.GetRoutesInfo();

                    // callSign
                    if (airportsInfo != null && routesInfo != null && selectedAircraft.FlightNum.Trim() != "")
                    {
                        var selectedRoute = routesInfo.FirstOrDefault(dict => dict.ContainsKey("Callsign") && dict["Callsign"] == selectedAircraft.FlightNum.Trim());

                        if (selectedRoute != null)
                        {
                            var airportCodes = selectedRoute["AirportCodes"].Split('-');
                            _trackHook.DepartureAirport = airportsInfo.FirstOrDefault(dict => dict.ContainsKey("ICAO") && dict["ICAO"] == airportCodes[0]);
                            _trackHook.ArrivalAirport = airportsInfo.FirstOrDefault(dict => dict.ContainsKey("ICAO") && dict["ICAO"] == airportCodes[1]);
                        }
                    }

                }
                else
                {
                    _trackHook.Valid_CPA = true;
                    _trackHook.ICAO_CPA = selectedAircraft.ICAO;
                    _trackHook.DepartureAirport = null;
                    _trackHook.ArrivalAirport = null;
                }
            }
            else
            {
                if (!cpaHook)
                {
                    _trackHook.Valid_CC = false;
                    //ICAOLabel.Text = "N/A";
                    //FlightNumLabel.Text = "N/A";
                    //CLatLabel.Text = "N/A";
                    //CLonLabel.Text = "N/A";
                    //SpdLabel.Text = "N/A";
                    //HdgLabel.Text = "N/A";
                    //AltLabel.Text = "N/A";
                    //MsgCntLabel.Text = "N/A";
                    //TrkLastUpdateTimeLabel.Text = "N/A";
                    _trackHook.DepartureAirport = null;
                    _trackHook.ArrivalAirport = null;
                }
                else
                {
                    //TrackHook.Valid_CPA = false;
                    //CpaTimeValue.Text = "None";
                    //CpaDistanceValue.Text = "None";
                }
            }
        }

        private void glControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            GL.Viewport(0, 0, glControl.ActualWidth > 0 ? (int)glControl.ActualWidth : 1, glControl.ActualHeight > 0 ? (int)glControl.ActualHeight : 1);
            GL.Disable(EnableCap.DepthTest); // 깊이 테스트 비활성화 (2D 렌더링)
            GL.Enable(EnableCap.Blend); // 알파 블렌딩 활성화
            GL.Enable(EnableCap.LineStipple); // 선 생략 기능 활성화
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One); // 알파 블렌딩 함수 설정
            _earthView.Resize((int)e.NewSize.Width, (int)e.NewSize.Height);

            UpdateRegion(); // 현재 지역 업데이트
        }

        private bool _isLoaded = false;
        private int _MouseLeftDownX;
        private int _MouseLeftDownY;
        private int _MouseDownMask;
        
        private const int LEFT_MOUSE_DOWN = 1; // 마우스 왼쪽 버튼 클릭 상태 플래그
        private const int RIGHT_MOUSE_DOWN = 2; // 마우스 오른쪽 버튼 클릭 상태 플래그
        private const int MIDDLE_MOUSE_DOWN = 4; // 마우스 가운데 버튼 클릭 상태 플래그

        private Area _areaTemp; // 폴리곤 영역 임시 저장용. 나중에 하자
        private StreamReader _playbackSbsStream;

        private void glControl_Render(TimeSpan obj)
        {
            if (!_isLoaded)
                return;

            if ((bool)cboxDrawMap.IsChecked)
                GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f); // 검은색 배경
            else
                GL.ClearColor(BG_INTENSITY, BG_INTENSITY, BG_INTENSITY, 0.0f); // 배경색 강도에 따라 설정

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit); // 화면 지우기

            _earthView.Animate(); // 애니메이션 업데이트
            _earthView.Render((bool)cboxDrawMap.IsChecked); // 지도 렌더링
            _tileManager.Cleanup(); // 타일 매니저 정리

            DrawObject(); // OpenGL 객체 그리기
        }

        private void UpdateRegion()
        {
            var rgn = _earthView.PreRender(); // 렌더링 전 준비 작업

            Map_v[0] = rgn.V[0];
            Map_v[1] = rgn.V[1];
            Map_v[2] = rgn.V[2];
            Map_v[3] = rgn.V[3];
            Map_w[0] = rgn.W[0];
            Map_w[1] = rgn.W[1];
            Map_p[0] = rgn.P[0];
            Map_p[1] = rgn.P[1];
            Map_p[2] = rgn.P[2];
            Map_p[3] = rgn.P[3];

            Mw1 = Map_w[1].X - Map_w[0].X; // 지도 너비 계산
            Mw2 = Map_v[1].X - Map_v[0].X; // 지도 높이 계산
            Mh1 = Map_w[1].Y - Map_w[0].Y; // 가상 좌표 높이 계산
            Mh2 = Map_v[3].Y - Map_v[0].Y; // 가상 좌표 너비 계산

            xf = Mw1 / Mw2; // 가상 좌표 너비 대비 지도 너비 비율
            yf = Mh1 / Mh2; // 가상 좌표 높이 대비 지도 높이 비율
        }

        private void DrawObject()
        {
            int viewableAircraft = 0;
            double scrX, scrY;

            // --- 1. OpenGL 상태 설정 ---
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.PointSmooth);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            GL.LineWidth(3.0f);
            GL.PointSize(4.0f);
            GL.Color4(1.0f, 1.0f, 1.0f, 1.0f);

            // --- 2. 공항 정보 그리기 ---
            List<Dictionary<string, string>> airportsInfo = AirportDB.GetAirPortsInfo();
            HashSet<string> uniqueAirports = AirportDB.GetUniqueAirportCodes();

            if (airportsInfo != null)
            {
                foreach (var row in airportsInfo)
                {
                    string icao = row["ICAO"];
                    if (uniqueAirports.Contains(icao) &&
                        double.TryParse(row["Latitude"], out double lat) &&
                        double.TryParse(row["Longitude"], out double lon))
                    {
                        if (lat > 85.0511 || lat < -85.0511) continue;

                        LatLon2XY(lat, lon, out scrX, out scrY);

                        // 화면 밖 객체는 그리지 않음 (Culling)
                        if (scrX < 0 || scrX > glControl.ActualWidth || scrY < 0 || scrY > glControl.ActualHeight)
                        {
                            continue;
                        }
                        Ntds2d.DrawAirportVBO(scrX, scrY, airplaneScale);
                    }
                }
            }

            // --- 3. 항공기 정보 그리기 ---
            var aircraftTable = AircraftManager.GetAll();
            foreach (var data in aircraftTable)
            {
                if (!data.HaveLatLon) continue;
                viewableAircraft++;

                LatLon2XY(data.Latitude, data.Longitude, out scrX, out scrY);

                // 화면 밖에 있는 객체는 그리지 않음
                if (scrX < -50 || scrX > glControl.ActualWidth + 50 || scrY < -50 || scrY > glControl.ActualHeight + 50)
                {
                    continue;
                }

                if (data.HaveSpeedAndHeading)
                    GL.Color4(1f, 0f, 1f, 1f);
                else
                {
                    data.Heading = 0;
                    GL.Color4(1f, 0f, 0f, 1f);
                }

                Ntds2d.DrawAirplaneImage(scrX, scrY, airplaneScale, data.Heading, data.SpriteImage);

                // Time To Go 선 그리기
                if (data.HaveSpeedAndHeading && TimeToGoCheckBox.IsChecked == true)
                {
                    if (LatLonConv.VDirect(data.Latitude, data.Longitude,
                                          data.Heading, data.Speed / 3060.0 * TimeToGoSlider.Value,
                                          out double lat2, out double lon2, out _) == TCoordConvStatus.OKNOERROR)
                    {
                        LatLon2XY(lat2, lon2, out double scrX2, out double scrY2);
                        GL.Color4(1.0, 1.0, 0.0, 1.0);
                        GL.Begin(PrimitiveType.Lines);
                        GL.Vertex2(scrX, scrY);
                        GL.Vertex2(scrX2, scrY2);
                        GL.End();
                    }
                }
            }

            // --- 4. 선택된 항공기(TrackHook) 정보 처리 ---
            // UI 업데이트를 위한 데이터를 담을 임시 변수
            Action uiUpdateAction = null;

            if (_trackHook.Valid_CC)
            {
                if (AircraftManager.TryGet(_trackHook.ICAO_CC, out var data))
                {
                    // 화면에 선택 표시 그리기
                    if (data.HaveLatLon)
                    {
                        LatLon2XY(data.Latitude, data.Longitude, out scrX, out scrY);
                        Ntds2d.DrawTrackHook(scrX, scrY, airplaneScale * 0.5);
                    }

                    // UI 업데이트 로직을 Action에 저장
                    uiUpdateAction = () =>
                    {
                        IcaoText.Text = data.HexAddr;
                        FlightText.Text = data.HaveFlightNum ? data.FlightNum : "N/A";
                        SelLatText.Text = data.HaveLatLon ? DMS.DegreesMinutesSecondsLat(data.Latitude) : "N/A";
                        SelLonText.Text = data.HaveLatLon ? DMS.DegreesMinutesSecondsLon(data.Longitude) : "N/A";
                        SpeedText.Text = data.HaveSpeedAndHeading ? $"{data.Speed:F2} KTS  VRATE: {data.VerticalRate:F2}" : "N/A";
                        HdgText.Text = data.HaveSpeedAndHeading ? $"{data.Heading:F2} DEG" : "N/A";
                        AltText.Text = data.Altitude > 0 ? $"{data.Altitude:F2} FT" : "N/A";
                        RawCnt.Text = data.NumMessagesRaw.ToString();
                        SbsCnt.Text = data.NumMessagesSBS.ToString();
                    };
                }
                else
                {
                    _trackHook.Valid_CC = false; // 데이터가 없으면 유효하지 않음 처리
                }
            }

            // 만약 선택된 항공기가 없다면 UI를 N/A로 초기화
            if (!_trackHook.Valid_CC)
            {
                uiUpdateAction = () =>
                {
                    IcaoText.Text = "N/A";
                    FlightText.Text = "N/A";
                    SelLatText.Text = "N/A";
                    SelLonText.Text = "N/A";
                    SpeedText.Text = "N/A";
                    HdgText.Text = "N/A";
                    AltText.Text = "N/A";
                    RawCnt.Text = "N/A";
                    SbsCnt.Text = "N/A";
                };
            }

            // --- 5. 선택된 항공기의 출발/도착 공항 정보 그리기 ---
            if (_trackHook.DepartureAirport != null && _trackHook.ArrivalAirport != null)
            {
                if (double.TryParse(_trackHook.DepartureAirport["Latitude"], out double ddLat) && double.TryParse(_trackHook.DepartureAirport["Longitude"], out double ddLon) &&
                    double.TryParse(_trackHook.ArrivalAirport["Latitude"], out double daLat) && double.TryParse(_trackHook.ArrivalAirport["Longitude"], out double daLon))
                {
                    LatLon2XY(ddLat, ddLon, out double dScrX, out double dScrY);
                    LatLon2XY(daLat, daLon, out double aScrX, out double aScrY);

                    Ntds2d.DrawLinkedPointsWithCircles(dScrX, dScrY, aScrX, aScrY);
                }
            }

            // --- 6. 최종 UI 업데이트 (안전한 방식) ---
            // 렌더링 스레드에서 직접 UI를 건드리지 않고, UI 스레드에서 실행하도록 작업을 전달합니다.
            Application.Current.Dispatcher.Invoke(() =>
            {
                uiUpdateAction?.Invoke();
                NumAircraftText.Text = viewableAircraft.ToString();
            });
        }

        private void XYtoLatLon(double scrX, double scrY, out double lat, out double lon)
        {
            // 1. 카메라(뷰)의 현재 상태를 가져옵니다.
            double aspect = glControl.ActualWidth / glControl.ActualHeight;
            if (double.IsNaN(aspect) || aspect == 0) aspect = 1.0;

            double eyeNormX = _earthView.Eye.X;
            double eyeNormY = _earthView.Eye.Y;

            double xSpanNorm = _earthView.Eye.XSpan(aspect);
            double ySpanNorm = _earthView.Eye.YSpan(aspect);

            if (xSpanNorm == 0 || ySpanNorm == 0)
            {
                lat = 0; lon = 0;
                return;
            }

            // 2. 화면 좌표를 정규화된 델타 좌표로 변환합니다.
            double deltaNormX = (scrX - (glControl.ActualWidth / 2.0)) * xSpanNorm / glControl.ActualWidth;
            double deltaNormY = (scrY - (glControl.ActualHeight / 2.0)) * ySpanNorm / glControl.ActualHeight;

            // 3. 정규화된 월드 좌표를 계산합니다.
            double objectNormX = deltaNormX + eyeNormX;
            double objectNormY = deltaNormY + eyeNormY;

            // 4. 정규화된 월드 좌표를 위도/경도로 변환합니다. (Web Mercator 역산)
            lon = objectNormX * 360.0;
            lat = Math.Atan(Math.Sinh(objectNormY * 2.0 * Math.PI)) * (180.0 / Math.PI);
        }

        // In MainWindow.xaml.cs
        private void LatLon2XY(double lat, double lon, out double scrX, out double scrY)
        {
            // 1. 카메라(뷰)의 현재 상태를 가져옵니다.
            double aspect = glControl.ActualWidth / glControl.ActualHeight;
            if (double.IsNaN(aspect) || aspect == 0) aspect = 1.0;

            double eyeNormX = _earthView.Eye.X;
            double eyeNormY = _earthView.Eye.Y;

            double xSpanNorm = _earthView.Eye.XSpan(aspect);
            double ySpanNorm = _earthView.Eye.YSpan(aspect);

            if (xSpanNorm == 0 || ySpanNorm == 0)
            {
                scrX = -1; scrY = -1;
                return;
            }

            // 2. 객체의 위도/경도를 정규화된 월드 좌표로 변환합니다.
            double objectNormX = lon / 360.0;

            double siny = Math.Sin(lat * Math.PI / 180.0);
            siny = Math.Min(Math.Max(siny, -0.9999), 0.9999);
            double objectNormY = Math.Log((1.0 + siny) / (1.0 - siny)) / (4.0 * Math.PI);

            // 3. 카메라와 객체 사이의 가장 짧은 거리를 계산합니다.
            double deltaNormX = objectNormX - eyeNormX;
            if (deltaNormX > 0.5) { deltaNormX -= 1.0; }
            else if (deltaNormX < -0.5) { deltaNormX += 1.0; }

            double deltaNormY = objectNormY - eyeNormY;

            // 4. 최종 화면 좌표로 변환합니다. Y축 뒤집기 로직을 제거하여 표준 OpenGL 좌표계에 맞춥니다.
            scrX = (deltaNormX / xSpanNorm) * glControl.ActualWidth + (glControl.ActualWidth / 2.0);
            scrY = (deltaNormY / ySpanNorm) * glControl.ActualHeight + (glControl.ActualHeight / 2.0);
        }

        private double WrapLongitude(double lon)
        {
            lon = (lon + 180) % 360;
            if (lon < 0) lon += 360;
            return lon - 180;
        }

        // 화면 좌표를 위도/경도로 변환
        private int XY2LatLon2(int x, int y, out double lat, out double lon)
        {
            // Y축 뒤집기 (WPF 좌표계 기준)
            int Y1 = (int)(glControl.ActualHeight - 1) - y;
            int X1 = x;

            if (X1 < Map_v[0].X || X1 > Map_v[1].X ||
                Y1 < Map_v[0].Y || Y1 > Map_v[3].Y) {
                lat = 0;
                lon = 0;
                return -1;
            }

            lat = Math.Atan(Math.Sinh(Math.PI * (2 * (Map_w[1].Y - (yf * (Map_v[3].Y - Y1)))))) * (180.0 / Math.PI);
            lon = (Map_w[1].X - (xf * (Map_v[1].X - X1))) * 360.0;

            return 0;
        }

        private void MapProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedItem = comboBox.SelectedItem as ComboBoxItem;
            string selectedText = selectedItem?.Content.ToString();

            Console.WriteLine($"선택된 맵: {selectedText}");

            // 여기에 map provider 변경 로직을 작성하세요
            switch (selectedText) {
                case "Google Maps":
                    LoadMap(TileServerType.GoogleMaps);
                    break;
                case "Bing Maps":
                    LoadMap(TileServerType.SkyVector_VFR);
                    break;
                case "OpenStreetMap":
                    LoadMap(TileServerType.OpenStreet);
                    break;
                default:
                    LoadMap(TileServerType.GoogleMaps);
                    break;
            }
            UpdateRegion(); // 현재 지역 업데이트
        }

        /// <summary>
        /// Initializes map storage and layers based on the selected server type.
        /// Mirrors TForm1::LoadMap(int Type).
        /// </summary>
        public void LoadMap(TileServerType type)
        {
            // Determine base directory
            var homeDir = $"{Directory.GetCurrentDirectory()}\\Map";
            string subfolder;
            switch (type) {
                case TileServerType.GoogleMaps: subfolder = "GoogleMap"; break;
                case TileServerType.SkyVector_VFR: subfolder = "VFR_Map"; break;
                case TileServerType.SkyVector_IFR_Low: subfolder = "IFR_Low_Map"; break;
                case TileServerType.SkyVector_IFR_High: subfolder = "IFR_High_Map"; break;
                case TileServerType.OpenStreet: subfolder = "OpenStreetMap"; break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }

            /*
            // Append Live suffix if needed
            if (_loadMapFromInternet)
                subfolder += "_Live";
            */

            var cacheDir = Path.Combine(homeDir, subfolder);
            Directory.CreateDirectory(cacheDir);

            // Initialize filesystem storage
            _storage = new FileSystemStorage(cacheDir, useGe: true);

            // If using internet, chain keyhole connection
            if (_loadMapFromInternet) {
                _keyhole = new KeyholeConnection(type);
                _keyhole.SetSaveStorage(_storage);
                _storage.SetNextLoadStorage(_keyhole);
            }

            // TileManager and rendering layers
            _tileManager = new TileManager(_storage);
            _masterLayer = new GoogleLayer(_tileManager);
            _earthView = new FlatEarthView(_masterLayer);

            // Resize view to current control size
            _earthView.Resize((int)glControl.ActualWidth, (int)glControl.ActualHeight);
        }
    }

    public class AreaOfInterest
    {
        public string AreaName { get; set; }
        public SolidColorBrush AreaColorBrush { get; set; }
    }
}
