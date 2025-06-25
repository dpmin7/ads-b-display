using ADS_B_Display.Map;
using ADS_B_Display.Map.MapSrc;
using ADS_B_Display.Models;
using ADS_B_Display.Models.Settings;
using ADS_B_Display.Properties;
using ADS_B_Display.Utils;
using ADS_B_Display.Views.Popup;
using ADS_B_Display.Views.UserControls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ADS_B_Display.Views
{
    internal class AircraftControlViewModel : NotifyPropertyChangedBase, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetLogger("AircraftControlViewModel");

        private SbsWorker _sbsWorker = null;
        private SbsWorker _rawWorker = null;


        public AircraftControlViewModel()
        {
            _sbsWorker = new SbsWorker(AircraftManager.ReceiveSBSMessage);
            _rawWorker = new SbsWorker(AircraftManager.ReceiveRawMessage);

            RegisterEvents();

            ControlSettings = Setting.Instance.ControlSettings;
            OnChangeSetting();

            if (Enum.TryParse(ControlSettings.MapProvider, true, out TileServerType res))
                SelectedTileServer = res;
            else
                SelectedTileServer = TileServerType.GoogleMaps; // 기본값 설정

            Cmd_RawConnect = new DelegateCommand(RawConnect, CanRawConnect);
            Cmd_RawDisconnect = new DelegateCommand(RawDisconnect, CanRawDisconnect);
            Cmd_SbsConnect = new DelegateCommand(SbsConnect, CanSbsConnect);
            Cmd_SbsDisconnect = new DelegateCommand(SbsDisconnect, CanSbsDisconnect);

            Cmd_RawRecord = new DelegateCommand(RawRecord, CanRawRecord);
            Cmd_RawRecordStop = new DelegateCommand(RawRecordStop, CanRawRecordStop);

            Cmd_RawPlay = new DelegateCommand(RawPlay, CanRawPlay);
            Cmd_RawPlayStop = new DelegateCommand(RawPlayStop, CanRawPlayStop);

            Cmd_SbsRecord = new DelegateCommand(SbsRecord, CanSbsRecord);
            Cmd_SbsRecordStop = new DelegateCommand(SbsRecordStop, CanSbsRecordStop);

            Cmd_SbsPlay = new DelegateCommand(SbsPlay, CanSbsPlay);
            Cmd_SbsPlayStop = new DelegateCommand(SbsPlayStop, CanSbsPlayStop);

            Cmd_Purge = new DelegateCommand(Purge);

            Cmd_PolygonComplete = new DelegateCommand(PolygonComplete);
        }

        private void PolygonComplete(object obj)
        {
            AreaRegisterPopup popup = new AreaRegisterPopup();
            popup.Owner = Application.Current.MainWindow;
            var res = popup.ShowDialog();
            if (res == true) {
                var name = popup.AreaName;
                var color = popup.AreaColor;
            }
        }

        private bool _isRawRecording = false;
        private bool _isRawPalying = false;
        private bool _isSbsRecording = false;
        private bool _isSbsPalying = false;

        private bool CanRawRecord(object obj) => true;//RawConnectStatus == ConnectStatus.Connect; // connect 시에 _isRecord, isPlaying, isPlayStopped 초기화
        private bool CanRawRecordStop(object obj) => true;//RawConnectStatus == ConnectStatus.Connect; // connect 시에 _isRecord, isPlaying, isPlayStopped 초기화
        private bool CanRawPlay(object obj) => true;//RawConnectStatus == ConnectStatus.Disconnect && ;
        private bool CanRawPlayStop(object obj) => true;// !_isRawRecording;
        private bool CanSbsRecord(object obj) => true;//!_isRawRecording;
        private bool CanSbsRecordStop(object obj) => true;//!_isRawRecording;
        private bool CanSbsPlay(object obj) => true;//!_isRawRecording;
        private bool CanSbsPlayStop(object obj) => true;//!_isRawRecording;

        public void Dispose()
        {
            ControlSettings.MapProvider = SelectedTileServer.ToString();
            Setting.Instance.ControlSettings = ControlSettings;
        }

        private void RawRecord(object obj)
        {
            if (RawConnectStatus != ConnectStatus.Connect)
                return;

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
                _rawWorker.RecordOn(path, false);
            } catch (Exception ex) {
                MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _rawWorker.RecordOff();
                return;
            }

            // 4) 녹화를 시작했다는 안내 메시지 (선택 사항)
            MessageBox.Show($"Start Raw record:\n{path}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            _isRawRecording = true;
        }

        private void RawRecordStop(object obj)
        {
            if (_isRawRecording == false)
                return;

            try {
                _rawWorker.RecordOff();
                _isRawRecording = false;
            } catch (Exception ex) {
                MessageBox.Show($"Error occur while Raw record file is closing.:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RawPlay(object obj)
        {
            if (_isRawPalying == true)
                return;

            var dlg = new OpenFileDialog {
                Title = "Raw Record file save",
                Filter = "Raw Log (*.raw)|*.raw|모든 파일 (*.*)|*.*",
                FileName = $"RawLog_{DateTime.Now:yyyyMMdd_HHmmss}.raw", // 기본 파일명 예시
                DefaultExt = ".raw"
            };

            if (dlg.ShowDialog() == true) {
                string fileName = dlg.FileName;
                if (!File.Exists(fileName)) {
                    MessageBox.Show("File " + fileName + " does not exist");
                } else {
                    try {
                        _rawWorker.Start(fileName);
                        _isRawPalying = true;
                    } catch (Exception ex) {
                        MessageBox.Show("Cannot open file " + fileName + "\n" + ex.Message);
                    }
                }
            }
        }

        private void RawPlayStop(object obj)
        {
            if (_isRawPalying == false)
                return;

            _rawWorker.Stop();
            _isRawPalying = false;
        }

        private void SbsRecord(object obj)
        {   
            if (SbsConnectStatus != ConnectStatus.Connect) {
                MessageBox.Show("SBS is not connected.");
                return;
            }

            string path = "";

            if (ControlSettings.UseBigQuery)
            {
                // 2-1) BigQuery 이용하여 녹화
                try
                {
                    _sbsWorker.RecordOn(path, ControlSettings.UseBigQuery);
                } catch (Exception ex)
                {
                    MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _sbsWorker.RecordOff();
                    return;
                }

                // 3) 녹화를 시작했다는 안내 메시지 (선택 사항)
                MessageBox.Show($"Start SBS record:\nQuery", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // 2-2) 기록 중이 아니면, SaveFileDialog 띄워서 파일 경로 선택
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

                path = dlg.FileName;
                try {
                    _sbsWorker.RecordOn(path);
                } catch (Exception ex) {
                    MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _sbsWorker.RecordOff();
                    return;
                }

                // 4) 녹화를 시작했다는 안내 메시지 (선택 사항)
                MessageBox.Show($"Start SBS record:\n{path}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _isSbsRecording = true;
        }

        private void SbsRecordStop(object obj)
        {
            if (_isSbsRecording == false)
                return;

            try {
                _sbsWorker.RecordOff(ControlSettings.UseBigQuery);
                _isSbsRecording = false;
            } catch (Exception ex) {
                MessageBox.Show($"SBS 기록 파일을 닫는 동안 오류가 발생했습니다:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SbsPlay(object obj)
        {
            if (_isSbsPalying == true)
                return;

            if (ControlSettings.UseBigQuery)
            {
                List<BigQueryListItem> items = new List<BigQueryListItem>();
                int temp = 100;
                DateTime now = DateTime.Now;
                for (int i = 0; i < 5; i++) {

                    BigQueryListItem item = new BigQueryListItem(DateTime.Now.AddSeconds(i*100), DateTime.Now.AddSeconds((i+1)*100));
                    items.Add(item);
                }
                var win = new BigQueryListPopup(items);
                var res = win.ShowDialog();
                if (res == false)
                    return;

                var selItem = win.SelectedItem;

                _sbsWorker.Start(null, true);
            }
            else
            {
                var dlg = new OpenFileDialog {
                    Title = "SBS Record file save",
                    Filter = "SBS Log (*.sbs)|*.sbs|모든 파일 (*.*)|*.*",
                    FileName = $"SbsLog_{DateTime.Now:yyyyMMdd_HHmmss}.sbs", // 기본 파일명 예시
                    DefaultExt = ".sbs"
                };

                if (dlg.ShowDialog() == true) {
                    string fileName = dlg.FileName;
                    if (!File.Exists(fileName)) {
                        MessageBox.Show("File " + fileName + " does not exist");
                    } else {
                        try {
                            _sbsWorker.Start(fileName);
                        } catch (Exception ex) {
                            MessageBox.Show("Cannot open file " + fileName + "\n" + ex.Message);
                        }
                    }
                }
            }

            _isSbsPalying = true;
        }

        private void SbsPlayStop(object obj)
        {
            if (_isSbsPalying == false)
                return;

            _sbsWorker.Stop(ControlSettings.UseBigQuery);
            _isSbsPalying = false;
        }

        private void RegisterEvents()
        {
            EventBus.Observe(EventIds.EvtAircraftHooked).Subscribe(msg => UpdateHookedAircraft(msg));
            EventBus.Observe(EventIds.EvtMouseMoved).Subscribe(msg => UpdateMouseMove(msg));
        }

        private void UpdateMouseMove(object msg)
        {
            (double lat, double lon) mouse = ((double lat, double lon))msg;
            LatitudeOfMouse = DMS.DegreesMinutesSecondsLat(mouse.lat);
            LongitudeOfMouse = DMS.DegreesMinutesSecondsLon(mouse.lon);
        }

        private bool CanRawConnect(object obj) => RawConnectStatus == ConnectStatus.Disconnect;
        private bool CanRawDisconnect(object obj) => RawConnectStatus == ConnectStatus.Connect || SbsConnectStatus == ConnectStatus.Error;
        private bool CanSbsConnect(object obj) => SbsConnectStatus == ConnectStatus.Disconnect;
        private bool CanSbsDisconnect(object obj) => SbsConnectStatus == ConnectStatus.Connect || SbsConnectStatus == ConnectStatus.Error;

        public ICommand Cmd_RawConnect { get; }
        public ICommand Cmd_RawDisconnect { get; }
        public ICommand Cmd_SbsConnect { get; }
        public ICommand Cmd_SbsDisconnect { get; }

        public ICommand Cmd_RawRecord { get; }
        public ICommand Cmd_RawRecordStop { get; }
        public ICommand Cmd_RawPlay { get; }
        public ICommand Cmd_RawPlayStop { get; }
        public ICommand Cmd_SbsRecord { get; }
        public ICommand Cmd_SbsRecordStop { get; }
        public ICommand Cmd_SbsPlay { get; }
        public ICommand Cmd_SbsPlayStop { get; }
        public ICommand Cmd_Purge { get; }
        public ICommand Cmd_PolygonComplete { get; }
        //

        private void RawDisconnect(object obj)
        {
            if (RawConnectStatus == ConnectStatus.Disconnect)
                return;

            try {
                _rawWorker.Stop();
                RawConnectStatus = ConnectStatus.Disconnect;
            } catch { }
        }

        private async void RawConnect(object obj)
        {
            if (RawConnectStatus == ConnectStatus.Connect)
                return;

            // 연결 중이 아니면 TextBox에 입력된 host:port로 연결 시도
            string input = ControlSettings.RawAddress.Trim();
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
                var popup = new LoadingPopup() { WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                                 Owner = Application.Current.MainWindow };
                popup.Closed += (s, e2) => { if (popup.IsCancelled) cts.Cancel(); };
                popup.Show();
                RawConnectStatus = ConnectStatus.Error;
                var res = await _rawWorker.Start(host, port, cts.Token);
                if (res) {
                    popup.Close();
                    RawConnectStatus = ConnectStatus.Connect;
                } else {
                    MessageBox.Show("Connection Timeout.");
                }
            } catch (Exception ex) {
                logger.Error(ex);
                _rawWorker.Stop();
                RawConnectStatus = ConnectStatus.Disconnect;
            }
        }

        private void SbsDisconnect(object obj)
        {
            if (SbsConnectStatus == ConnectStatus.Disconnect)
                return;

            try {
                _sbsWorker.Stop();
                SbsConnectStatus = ConnectStatus.Disconnect;
            } catch { }
#if false // PingEcho 테스트용 코드 (필요시 활성화)           
                pingEcho.Stop();
#endif
        }

        private async void SbsConnect(object obj)
        {
            if (SbsConnectStatus == ConnectStatus.Connect)
                return;

            string input = ControlSettings.SbsAddress.Trim();
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
                var popup = new LoadingPopup() {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow
                };
                popup.Closed += (s, e2) => { if (popup.IsCancelled) cts.Cancel(); };
                SbsConnectStatus = ConnectStatus.Error;
                popup.Show();
                var res = await _sbsWorker.Start(host, port, cts.Token);
                if (res) {
                    popup.Close();
                    SbsConnectStatus = ConnectStatus.Connect;
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
                } else {
                    MessageBox.Show("Connection Timeout.");
                }
            } catch (Exception ex) {
                logger.Error(ex);
                _sbsWorker.Stop();
                SbsConnectStatus = ConnectStatus.Disconnect;
            }
        }


        private void UpdateHookedAircraft(object msg)
        {
            if (msg == null) {
                Aircraft = null;
                return;
            }

            if (msg is TrackHookStruct hookedAc && AircraftManager.TryGet(hookedAc.ICAO_CC, out Aircraft ac))
            {
                if (Aircraft == null) {
                    Aircraft = new AircraftForUI();
                }
                Aircraft.UpdateAircraftForUI(ac);
            }
        }

        private void Purge(object obj)
        {
            AircraftManager.PurgeAll();
        }

        private AircraftForUI aircraft = null;
        public AircraftForUI Aircraft { get => aircraft; set => SetProperty(ref aircraft, value); }

        private ConnectStatus rawConnectStatus = ConnectStatus.Disconnect;
        public ConnectStatus RawConnectStatus { get => rawConnectStatus; set => SetProperty(ref rawConnectStatus, value); }

        private ConnectStatus sbsConnectStatus = ConnectStatus.Disconnect;
        public ConnectStatus SbsConnectStatus { get => sbsConnectStatus; set => SetProperty(ref sbsConnectStatus, value); }


        // Config
        public ControlSettings ControlSettings { get; set; } = new ControlSettings();

        public List<TileServerType> TileServerTypeList { get; set; } = Enum.GetValues(typeof(TileServerType)).Cast<TileServerType>().ToList();
        private TileServerType selectedTileServer = TileServerType.GoogleMaps;
        public TileServerType SelectedTileServer {
            get => selectedTileServer;
            set {
                SetProperty(ref selectedTileServer, value);
                MapManager.Instance.LoadMap(value);
            }
        }

        // Control
        private string latitudeOfMouse;
        public string LatitudeOfMouse { get => latitudeOfMouse; set => SetProperty(ref latitudeOfMouse, value); }

        private string longitudeOfMouse;
        public string LongitudeOfMouse { get => longitudeOfMouse; set => SetProperty(ref longitudeOfMouse, value); }

        public bool DisplayMapEnabled {
            get => ControlSettings.DisplayMapEnabled;
            set {
                ControlSettings.DisplayMapEnabled = value;
                OnPropertyChanged(nameof(DisplayMapEnabled));
                OnChangeSetting();
            }
        }

        private bool useTimeTogo;
        public bool UseTimeTogo {
            get => ControlSettings.UseTimeToGo;
            set {
                ControlSettings.UseTimeToGo = value;
                OnPropertyChanged(nameof(UseTimeTogo));
                OnChangeSetting();
            }
        }

        private double timeTogoValue;
        public double TimeTogoValue {
            get => ControlSettings.TimeToGoValue;
            set {
                ControlSettings.TimeToGoValue = value;
                OnPropertyChanged(nameof(TimeTogoValue));
                OnChangeSetting();
            }
        }

        private void OnChangeSetting()
        {
            EventBus.Publish(EventIds.EvtControlSettingChanged, ControlSettings);
        }

        // Display
        //private string icaoText;
        //public string IcaoText { get => icaoText; set => SetProperty(ref icaoText, value); }

        //private string flightText;
        //public string FlightText { get => flightText; set => SetProperty(ref flightText, value); }

        //private string latitudeStr;

        //public string LatitudeStr { get => latitudeStr; set => SetProperty(ref latitudeStr, value); }

        //private string longitudeStr;

        //public string LongitudeStr { get => longitudeStr; set => SetProperty(ref longitudeStr, value); }

        //private double speed;
        //public double Speed { get => speed; set => SetProperty(ref speed, value); }

        //private double vRate;
        //public double VRate { get => vRate; set => SetProperty(ref vRate, value); }

        //private double hdg;
        //public double Hdg { get => hdg; set => SetProperty(ref hdg, value); }

        //private double alt;
        //public double Alt { get => alt; set => SetProperty(ref alt, value); }

        //private long rawCnt;
        //public long RawCnt { get => rawCnt; set => SetProperty(ref rawCnt, value); }

        //private long sbsCnt;
        //public long SbsCnt { get => sbsCnt; set => SetProperty(ref sbsCnt, value); }
    }
}
