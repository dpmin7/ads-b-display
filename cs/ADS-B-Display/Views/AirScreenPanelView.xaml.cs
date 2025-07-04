﻿using ADS_B_Display.Map;
using ADS_B_Display.Map.MapSrc;
using ADS_B_Display.Models;
using ADS_B_Display.Models.Settings;
using ADS_B_Display.Utils;
using NLog;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SkiaSharp;

namespace ADS_B_Display.Views
{
    /// <summary>
    /// AirScreenPanelView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AirScreenPanelView : UserControl, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // ───────── Drag & Freeze 지원을 위한 신규 필드 ─────────
        private bool __isDragging;              // 왼쪽 버튼으로 PAN 중인지
        private bool _isDragging { get => __isDragging;
            set
            {
                __isDragging = value;
                Logger.Debug($"Dragging state changed: {__isDragging}");
                if (value)
                {
                    _freezeTexId = -1; // 드래그 시작 시 캡처된 텍스처 초기화
                }
            }
        }
        private bool _pendingCapture = false;
        private int _freezeTexId = -1;         // 캡처된 화면 텍스처
        private int _freezeW, _freezeH;        // 텍스처 크기

        private int _MouseLeftDownX;
        private int _MouseLeftDownY;
        private int _MouseDownMask;

        private const int LEFT_MOUSE_DOWN = 1; // 마우스 왼쪽 버튼 클릭 상태 플래그
        private const int RIGHT_MOUSE_DOWN = 2; // 마우스 오른쪽 버튼 클릭 상태 플래그
        private const int MIDDLE_MOUSE_DOWN = 4; // 마우스 가운데 버튼 클릭 상태 플래그

        private int _dragOffsetX = 0;
        private int _dragOffsetY = 0;

        // Map 관련 필드
        double Mw1, Mw2, Mh1, Mh2, xf, yf;
        public Vector3d[] Map_v = new Vector3d[4];
        public Vector3d[] Map_p = new Vector3d[4];
        public Vector2d[] Map_w = new Vector2d[2];
        double MapCenterLat, MapCenterLon;

        private const float BG_INTENSITY = 0.37f; // 배경색 강도 (0.0f ~ 1.0f)
        private const float MAP_CENTER_LAT = 40.73612f; // 지도 중심 위도
        private const float MAP_CENTER_LON = -80.33158f; // 지도 중심 경도

        private bool _useTimeToGo;
        private double _timeTogoValue;
        private bool _mapDisplay;

        private bool _isLoaded;
        private bool _allowDraw = true; // 그리기 허용 여부

        private FlatEarthView _earthView;

        private DispatcherTimer _updateTimer = new DispatcherTimer();
        private Timer _delayTimer;

        public static DateTime? connectStartTime = null;
        public static DateTime? msgReceiveTime = null;
        private static DateTime? firstAircraftDrawTime = null;

        public AirScreenPanelView()
        {
            InitializeComponent();
            EventBus.Observe(EventIds.EvtControlSettingChanged).Subscribe(msg => UpdateTimeToGo(msg));
            EventBus.Observe(EventIds.EvtCenterMapTo).Subscribe(msg => CenterMapTo(msg));
            EventBus.Observe(EventIds.EvtZoom).Subscribe(msg => ZoomControl((bool)msg));

            var settings = new GLWpfControlSettings()
            {
                MajorVersion = 2, // OpenGL Major Version
                MinorVersion = 1, // OpenGL Minor Version
                RenderContinuously = false
            };
            glControl.Start(settings);

            MapManager.Instance.RegisterLoadMapCallback(MapLoaded);
            MapManager.Instance.LoadMap(TileServerType.GoogleMaps);

            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += _updateTimer_Tick;
            _updateTimer.Start();

            _delayTimer = new Timer(200) { // 200ms 후에 그릴지 확인
                AutoReset = false, // 단발성 타이머
            };
            _delayTimer.Elapsed += (s, e) => {
                _allowDraw = true; // 타이머가 끝나면 그리기 허용
                glControl.InvalidateVisual(); // OpenGL 컨트롤 강제 갱신
            };

            // aircrafe load
            AircraftDB.Init();
        }

        public void Dispose()
        {
            _updateTimer?.Stop();
            _updateTimer = null;
            _delayTimer?.Stop();
            _delayTimer?.Dispose();
            _delayTimer = null;

            // OpenGL 리소스 해제
            Ntds2d.DisposeAllGLResources();

            Setting.Instance.MapConfig.EyeX = _earthView.Eye.X;
            Setting.Instance.MapConfig.EyeY = _earthView.Eye.Y;
            Setting.Instance.MapConfig.EyeH = _earthView.Eye.H;
        }

        private void UpdateTimeToGo(object msg)
        {
            var setting = (ControlSettings)msg;
            _useTimeToGo = setting.UseTimeToGo;
            _timeTogoValue = setting.TimeToGoValue;
            _mapDisplay = setting.DisplayMapEnabled;
        }

        private void _updateTimer_Tick(object sender, EventArgs e)
        {
            if (!_allowDraw || !_isLoaded) return; // 그리기 허용 여부 확인

            glControl.InvalidateVisual(); // OpenGL 컨트롤 강제 갱신
        }

        public void UpdateSetting(bool useTimeTogo, double timeTogoValue)
        {
            _useTimeToGo = useTimeTogo;
            _timeTogoValue = timeTogoValue;
        }

        private void MapLoaded(MasterLayer masterLayer, bool isDark)
        {
            if (isDark)
                altitudeImg.Source = new Uri("pack://application:,,,/Images/altitude_chart_Dark.svg");
            else
                altitudeImg.Source = new Uri("pack://application:,,,/Images/altitude_chart_Light.svg");

            _earthView = new FlatEarthView(masterLayer);
            _earthView.Resize((int)glControl.ActualWidth, (int)glControl.ActualHeight);

            UpdateRegion();
        }

        // 1. 드래그 시작 시점의 Eye.Y 저장
        private double _dragStartEyeY;
        private int _dragStartMouseY;
        private void glControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Mouse.Capture((UIElement)sender); // 마우스 캡처 설정
            Mouse.OverrideCursor = Cursors.Hand;      // 손 모양

            // 마우스 좌표 구하기 (정수형으로 변환)
            int x = (int)e.GetPosition(glControl).X;
            int y = (int)e.GetPosition(glControl).Y;

            // 마우스 왼쪽 버튼 클릭 확인
            if (e.ChangedButton == MouseButton.Left)
            {   
                _MouseLeftDownX = x;
                _MouseLeftDownY = y;
                _MouseDownMask |= LEFT_MOUSE_DOWN;
                _dragStartEyeY = _earthView.Eye.Y;      // 드래그 시작 Eye.Y 저장
                _dragStartMouseY = y;                   // 드래그 시작 마우스 Y 저장

                // Ctrl 키가 눌렸는지 확인
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    // Ctrl + 왼쪽 클릭 시 동작 (필요시 구현)
                }
                else
                {
                    _pendingCapture = true; // 캡처 플래그 설정만 수행
                    // EarthView의 StartDrag 호출
                    _earthView.StartDrag(x, y, EarthView.NAV_DRAG_PAN);
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                if (AreaManager.IsInsertMode)
                {
                    InsertPolygon(x, y);
                }
                else
                {
                    bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
                    HookTrack(x, y, ctrl);
                }
                //}
            }
            else if (e.ChangedButton == MouseButton.Middle)
            {
                //ResetXYOffset();
            }
        }

        private void glControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            // 마우스 위치 구하기
            var pos = e.GetPosition(glControl);
            int x = (int)pos.X;
            int y = (int)pos.Y;

            if ((_MouseDownMask & LEFT_MOUSE_DOWN) != 0 && _isDragging)
            {
                // 2. 실제 Eye.Y 변화량을 화면 픽셀로 환산
                double eyeYDelta = _earthView.Eye.Y - _dragStartEyeY;
                double pixelsPerEyeY = glControl.ActualHeight / (Map_w[1].Y - Map_w[0].Y); // 대략적인 변환
                _dragOffsetY = -(int)(-eyeYDelta * pixelsPerEyeY);

                _dragOffsetX = x - _MouseLeftDownX;
                //_dragOffsetY = y - _MouseLeftDownY;
                glControl.InvalidateVisual();
            }

            // 화면 좌표계 변환 (Y축 뒤집기)
            int X1 = x;
            int Y1 = (int)(glControl.ActualHeight - 1) - y;

            // Map_v, Map_w, xf, yf 등은 클래스 필드로 선언되어 있다고 가정
            if (Map_v != null && Map_w != null &&
                X1 >= Map_v[0].X && X1 <= Map_v[1].X &&
                Y1 >= Map_v[0].Y && Y1 <= Map_v[3].Y)
            {
                // 위도/경도 계산
                double VLat = Math.Atan(Math.Sinh(Math.PI * (2 * (Map_w[1].Y - (yf * (Map_v[3].Y - Y1)))))) * (180.0 / Math.PI);
                double VLon = (Map_w[1].X - (xf * (Map_v[1].X - X1))) * 360.0;

                // 위도/경도 표시 (예: Label 컨트롤 사용)
                EventBus.Publish(EventIds.EvtMouseMoved, (VLat, VLon));
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
            if ((_MouseDownMask & LEFT_MOUSE_DOWN) != 0)
            {
                UpdateRegion(); // 현재 지역 업데이트
                _earthView.Drag(_MouseLeftDownX, _MouseLeftDownY, x, y, EarthView.NAV_DRAG_PAN);
                glControl.InvalidateVisual(); // 화면 갱신
                //eyeX.Text = _earthView.Eye.X.ToString(); eyeY.Text = _earthView.Eye.Y.ToString(); eyeH.Text = _earthView.Eye.H.ToString();
                XY2LatLon2(0, 0, out double Lat, out double Lon);
                XY2LatLon2(glControl.ActualWidth-1, glControl.ActualHeight-1, out double Lat2, out double Lon2);
                

                //Left.Text = DMS.DegreesMinutesSecondsLat(Lat);
                //Right.Text = DMS.DegreesMinutesSecondsLat(Lat2);
                //Top.Text = DMS.DegreesMinutesSecondsLon(Lon);
                //Bottom.Text = DMS.DegreesMinutesSecondsLon(Lon2);
            }
        }
        
        private void glControl_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            Mouse.Capture(null);
            Mouse.OverrideCursor = null;      // 손 모양
            // 마우스 왼쪽 버튼이 떼어졌을 때만 처리
            if (e.ChangedButton == MouseButton.Left)
            {
                _MouseDownMask &= ~LEFT_MOUSE_DOWN;
                _isDragging = false;
                _dragOffsetX = 0;
                _dragOffsetY = 0;
                ReleaseFreezeTexture();
                glControl.InvalidateVisual();
            }
        }

        private void InsertPolygon(int x, int y)
        {
            double lat, lon;
            if (XY2LatLon2(x, y, out lat, out lon) == 0)
            {
                if (AreaManager.AddPointToTempArea(lat, lon))
                {

                }
                else
                {
                    MessageBox.Show("Max Area Points Reached");
                }
            }

            glControl.InvalidateVisual();
        }

        private int XY2LatLon2(double x, double y, out double lat, out double lon)
        {
            lat = 0;
            lon = 0;

            int X1 = (int)x;
            int Y1 = (int)((glControl.ActualHeight - 1) - y);

            if (X1 < Map_v[0].X || X1 > Map_v[1].X ||
                Y1 < Map_v[0].Y || Y1 > Map_v[3].Y)
            {
                return -1;
            }

            lat = Math.Atan(Math.Sinh(Math.PI * (2 * (Map_w[1].Y - (yf * (Map_v[3].Y - Y1)))))) * (180.0 / Math.PI);
            lon = (Map_w[1].X - (xf * (Map_v[1].X - X1))) * 360.0;

            return 0;
        }

        private void glControl_Loaded(object sender, RoutedEventArgs e)
        {
            MapInit();

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
            Ntds2d.MakeAirplaneImages();
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
            glControl.InvalidateVisual(); // 첫 프레임 수동 렌더링

            // font load
            // 폰트 리소스 로딩 (경로는 실제 파일 위치에 맞게 수정)
            string texturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "font.png");
            string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "font.fnt");
            Ntds2d.LoadFont(dataPath, texturePath);
        }

        private void MapInit()
        {
            double x, y, h;
            if (Setting.Instance.MapConfig.IsInitialState)
            {
                MapCenterLat = MAP_CENTER_LAT;
                MapCenterLon = MAP_CENTER_LON;
                SetMapCenter(out x, out y);
                h = 1 / Math.Pow(1.3, 18);
            }
            else
            {
                MapCenterLat = MAP_CENTER_LAT;
                MapCenterLon = MAP_CENTER_LON;
                x = Setting.Instance.MapConfig.EyeX;
                y = Setting.Instance.MapConfig.EyeY;
                h = Setting.Instance.MapConfig.EyeH;
            }
            _earthView.Eye.X = x;
            _earthView.Eye.Y = y;
            _earthView.Eye.H = h;

            UpdateRegion(); // 현재 지역 업데이트
            //eyeX.Text = _earthView.Eye.X.ToString(); eyeY.Text = _earthView.Eye.Y.ToString(); eyeH.Text = _earthView.Eye.H.ToString();
        }

        private double airplaneScale;
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 강제 복원 (freeze 해제)
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseFreezeTexture();
                Mouse.Capture(null);
            }

            if (e.Delta > 0)
                _earthView.SingleMovement(EarthView.NAV_ZOOM_IN);
            else
                _earthView.SingleMovement(EarthView.NAV_ZOOM_OUT);

            airplaneScale = Math.Min((0.05 / _earthView.Eye.H), 1.5); // 스케일 계산

            UpdateRegion(); // 현재 지역 업데이트
            glControl.InvalidateVisual(); // 마우스 휠 이벤트 후 강제 갱신
            //eyeX.Text = _earthView.Eye.X.ToString(); eyeY.Text = _earthView.Eye.Y.ToString(); eyeH.Text = _earthView.Eye.H.ToString();
        }

        private void ZoomControl(bool isZoomIn)
        {
            if (isZoomIn)
                _earthView.SingleMovement(EarthView.NAV_ZOOM_IN);
            else
                _earthView.SingleMovement(EarthView.NAV_ZOOM_OUT);

            airplaneScale = Math.Min((0.05 / _earthView.Eye.H), 1.5); // 스케일 계산

            UpdateRegion(); // 현재 지역 업데이트
            glControl.InvalidateVisual(); // 마우스 휠 이벤트 후 강제 갱신
            //eyeX.Text = _earthView.Eye.X.ToString(); eyeY.Text = _earthView.Eye.Y.ToString(); eyeH.Text = _earthView.Eye.H.ToString();
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
            //eyeX.Text = _earthView.Eye.X.ToString(); eyeY.Text = _earthView.Eye.Y.ToString(); eyeH.Text = _earthView.Eye.H.ToString();
        }

        private void glControl_Render(TimeSpan obj)
        {
            if (!_isLoaded) return;

            if (_isDragging && _freezeTexId != -1)
            {
                // 캡처된 지도+항공기 이미지만 표시
                GL.ClearColor(_mapDisplay ? 0f : BG_INTENSITY,
                              _mapDisplay ? 0f : BG_INTENSITY,
                              _mapDisplay ? 0f : BG_INTENSITY,
                              1f);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                DrawFreezeTexture();
                return;
            }

            // 평상시 실시간 렌더링
            GL.ClearColor(_mapDisplay ? 0f : BG_INTENSITY,
                          _mapDisplay ? 0f : BG_INTENSITY,
                          _mapDisplay ? 0f : BG_INTENSITY,
                          1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _earthView.Animate();
            _earthView.Render(_mapDisplay);
            MapManager.Instance.ClearTitleManager();
            DrawObject();

            if (_pendingCapture)
            {
                GL.Flush();
                _pendingCapture = false;
                CaptureFreezeTexture();
                //CaptureWideFreezeTexture();
            }

            /*
            // 2. 테스트를 위한 깨끗한 렌더링 환경 설정
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, glControl.ActualWidth, 0, glControl.ActualHeight, -1, 1);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.Disable(EnableCap.DepthTest);
            GL.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // 3. 폰트 텍스처 전체를 그리는 테스트 함수 호출
            Ntds2d.DrawEntireFontTextureForTest();
            */
        }

        private void HookTrack(int x, int y, bool cpaHook)
        {
            int Y1 = ((int)glControl.ActualHeight - 1) - y;
            int X1 = x;

            if ((X1 < Map_v[0].X) || (X1 > Map_v[1].X) ||
                (Y1 < Map_v[0].Y) || (Y1 > Map_v[3].Y)) return;

            double vLat = Math.Atan(Math.Sinh(Math.PI * (2 * (Map_w[1].Y - (yf * (Map_v[3].Y - Y1)))))) * (180.0 / Math.PI);
            double vLon = (Map_w[1].X - (xf * (Map_v[1].X - X1))) * 360.0;

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
                    AircraftManager.TrackHook.Valid_CC = true;
                    AircraftManager.TrackHook.ICAO_CC = selectedAircraft.ICAO;
                    //Console.WriteLine(AircraftDB.GetAircraftInfo(selectedAircraft.ICAO));

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
                            AircraftManager.TrackHook.DrawAirports = new List<Dictionary<string, string>>();

                            foreach (var airport in airportCodes)
                            {
                                AircraftManager.TrackHook.DrawAirports.Add(airportsInfo.FirstOrDefault(dict => dict.ContainsKey("ICAO") && dict["ICAO"] == airport));
                            }
                            
                            AircraftManager.TrackHook.DepartureAirport = airportsInfo.FirstOrDefault(dict => dict.ContainsKey("ICAO") && dict["ICAO"] == airportCodes[0]);
                            AircraftManager.TrackHook.ArrivalAirport = airportsInfo.FirstOrDefault(dict => dict.ContainsKey("ICAO") && dict["ICAO"] == airportCodes[AircraftManager.TrackHook.DrawAirports.Count - 1]);
                        } else
                        {
                            AircraftManager.TrackHook.DepartureAirport = null;
                            AircraftManager.TrackHook.ArrivalAirport = null;
                            AircraftManager.TrackHook.DrawAirports = null;
                        }
                    }
                }
                else
                {
                    AircraftManager.TrackHook.TimestampUtc = selectedAircraft.LastSeen;
                    AircraftManager.TrackHook.Valid_CPA = true;
                    AircraftManager.TrackHook.ICAO_CPA = selectedAircraft.ICAO;
                    AircraftManager.TrackHook.DepartureAirport = null;
                    AircraftManager.TrackHook.ArrivalAirport = null;
                    AircraftManager.TrackHook.DrawAirports = null;
                }
            }
            else
            {
                if (!cpaHook)
                {
                    AircraftManager.TrackHook.Valid_CC = false;
                    AircraftManager.TrackHook.DepartureAirport = null;
                    AircraftManager.TrackHook.ArrivalAirport = null;
                    AircraftManager.TrackHook.DrawAirports = null;
                }
                else
                {
                    //TrackHook.Valid_CPA = false;
                    //CpaTimeValue.Text = "None";
                    //CpaDistanceValue.Text = "None";
                }
            }

            if (AircraftManager.TrackHook.Valid_CC)
                glControl.InvalidateVisual();
        }

        private void DrawObject()
        {
            SetupOpenGLDefaults();
            DrawMapCenterCrosshair();       // 중앙 십자선
            DrawTempArea();                 // 임시 다각형
            DrawSavedAreas();               // 저장된 다각형 영역들
            DrawAirports();                 // 공항 정보
            DrawAircrafts();                // 항공기들
            DrawTrackHook();                // 현재 Hook된 항공기
            DrawFlightRoute();             // Hook된 항공기의 경로
        }

        private void SetupOpenGLDefaults()
        {
            // 선/포인트를 부드럽게
            GL.Enable(EnableCap.LineSmooth);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

            GL.Enable(EnableCap.PointSmooth);

            // 알파 블렌딩 설정
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // 기본 선, 포인트 설정
            GL.LineWidth(3.0f);
            GL.PointSize(4.0f);

            // 기본 색상 설정 (흰색)
            GL.Color4(1.0f, 1.0f, 1.0f, 1.0f);
        }

        private void DrawMapCenterCrosshair()
        {
            LatLon2XY(MapCenterLat, MapCenterLon, out double scrX, out double scrY);

            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex2(scrX - 20.0, scrY);
            GL.Vertex2(scrX + 20.0, scrY);
            GL.End();

            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex2(scrX, scrY - 20.0);
            GL.Vertex2(scrX, scrY + 20.0);
            GL.End();
        }

        private void DrawTempArea()
        {

            var tempArea = AreaManager.TempArea;
            if (tempArea == null || tempArea.NumPoints <= 0) return;

            GL.PushAttrib(AttribMask.ColorBufferBit | AttribMask.CurrentBit | AttribMask.LineBit | AttribMask.PointBit);
            GL.Disable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Blend); // 블렌딩 끄기
            GL.Disable(EnableCap.LineStipple); // 선 패턴 끄기

            GL.Color4(1f, 1f, 1f, 1f);
            GL.LineWidth(4f);
            GL.PointSize(4f);
            var adj = new Vector2d[tempArea.NumPoints];
            for (int i = 0; i < tempArea.NumPoints; i++)
                LatLon2XY(tempArea.Points[i].Y, tempArea.Points[i].X, out adj[i].X, out adj[i].Y);

            GL.Color4(1f, 1f, 1f, 1f);
            GL.Begin(PrimitiveType.Points);
            foreach (var pt in adj) GL.Vertex2(pt.X, pt.Y);
            GL.End();

            GL.Color4(1f, 1f, 1f, 1f);
            GL.Begin(PrimitiveType.LineStrip);
            double prevLon = tempArea.Points[0].X;
            double lat = 0;
            double lon = 0;
            for (int i = 0; i < tempArea.NumPoints; i++)
            {
                lat = tempArea.Points[i].Y;
                lon = tempArea.Points[i].X;
                if (i > 0)
                {
                    double diff = lon - prevLon;
                    if (diff > 180) lon -= 360;
                    else if (diff < -180) lon += 360;
                }
                LatLon2XY_NoWrap(lat, lon, out double x, out double y);
                GL.Vertex2(x, y);

                prevLon = lon; // ← 보정된 값을 저장
            }
            GL.End();

            GL.PopAttrib();
        }

        private void LatLon2XY_NoWrap(double lat, double lon, out double x, out double y)
        {
            // WrapLongitude 생략
            x = (Map_v[1].X - ((Map_w[1].X - (lon / 360.0)) / xf));
            y = Map_v[3].Y - (Map_w[1].Y / yf) +
                (MathExt.Asinh(Math.Tan(lat * Math.PI / 180.0)) / (2 * Math.PI * yf));
        }

        private void DrawSavedAreas()
        {
            GL.PushAttrib(AttribMask.CurrentBit);
            foreach (var area in AreaManager.Areas)
            {
                var color = area.Color;
                GL.Disable(EnableCap.Texture2D);
                GL.Enable(EnableCap.PolygonOffsetFill);
                GL.PolygonOffset(1.0f, 1.0f);
                GL.Color4(color.R / 255f, color.G / 255f, color.B / 255f, 0.3f);

                // 영역 내부 삼각형 채우기
                GL.Begin(PrimitiveType.Triangles);
                foreach (var tri in area.Triangles)
                {
                    double prevLon = area.Points[(int)tri[0]].X;
                    for (int k = 0; k < 3; k++)
                    {
                        int idx = (int)tri[k];
                        if (idx >= area.NumPoints) continue;

                        double lat = area.Points[idx].Y;
                        double lon = area.Points[idx].X;

                        if (k > 0)
                        {
                            double diff = lon - prevLon;
                            if (diff > 180) lon -= 360;
                            else if (diff < -180) lon += 360;
                        }

                        LatLon2XY_NoWrap(lat, lon, out double x, out double y);
                        GL.Vertex2(x, y);

                        prevLon = lon;
                    }
                }
                GL.End();
                GL.Disable(EnableCap.PolygonOffsetFill);

                if (area.Selected)
                {
                    GL.LineWidth(4f);
                    GL.PushAttrib(AttribMask.LineBit);
                    GL.LineStipple(3, 0xAAAA);
                }

                // 영역 외곽선
                GL.Color4(color.R / 255f, color.G / 255f, color.B / 255f, 1f);
                GL.Begin(PrimitiveType.LineLoop);
                if (area.NumPoints > 0)
                {
                    double prevLon = area.Points[0].X;

                    for (int j = 0; j < area.NumPoints; j++)
                    {
                        double lat = area.Points[j].Y;
                        double lon = area.Points[j].X;

                        if (j > 0)
                        {
                            double diff = lon - prevLon;
                            if (diff > 180) lon -= 360;
                            else if (diff < -180) lon += 360;
                        }

                        LatLon2XY_NoWrap(lat, lon, out double x, out double y);
                        GL.Vertex2(x, y);

                        prevLon = lon;
                    }
                }
                GL.End();

                if (area.Selected)
                {
                    GL.PopAttrib();
                    GL.LineWidth(2f);
                }
            }

            GL.PopAttrib();
        }

        private void DrawAirports()
        {
            if (_earthView.Eye.H > 0.025)
                return;

            var airportsInfo = AirportDB.GetAirPortsInfo();
            var uniqueAirports = AirportDB.GetUniqueAirportCodes();
            if (airportsInfo == null) return;

            foreach (var row in airportsInfo)
            {
                string icao = row["ICAO"];
                string latitude = row["Latitude"];
                string longitude = row["Longitude"];

                if (!uniqueAirports.Contains(icao)) continue;
                if (!double.TryParse(latitude, out double lat) || !double.TryParse(longitude, out double lon)) continue;
                if (lat > 85.0511 || lat < -85.0511) continue;

                LatLon2XY(lat, lon, out double x, out double y);

                Ntds2d.DrawAirportVBO(x, y, airplaneScale * 0.6, _earthView.Eye.H > 0.010 ? "" : row["Name"]);
            }
        }

        private void DrawAircrafts()
        {
            if (_isDragging) return;

            Logger.Debug("DrawAircrafts()");
            var aircraftTable = AircraftManager.GetAll();
            
            foreach (var data in aircraftTable)
            {
                //if (data.IsOnScreen(_earthView.Eye, _earthView.Xspan, _earthView.Yspan))
                //    continue; // 화면에 표시되지 않는 항공기는 건너뜀

                if (!data.HaveLatLon) continue;
                if (AreaManager.UsePolygon == true && data.Viewable == false) continue;
                if (data.Filtered) continue; // 필터링된 항공기는 건너뜀
                GL.PushAttrib(AttribMask.CurrentBit);
                GL.Color4(1f, 1f, 1f, 1f);
                LatLon2XY(data.VLatitude, data.VLongitude, out double scrX, out double scrY);

                if (data.HaveSpeedAndHeading)
                    GL.Color4(1f, 0f, 1f, 1f);
                else
                {
                    data.Heading = 0;
                    GL.Color4(1f, 0f, 0f, 1f);
                }

                var imageNum = data.SpriteImage;

                if (data.AircraftData != null)
                {
                    imageNum = data.AircraftData.AircraftImageNum;
                }

                // Time To Go 경로선 표시
                if (data.HaveSpeedAndHeading && _useTimeToGo && _earthView.Eye.H < 0.025)
                {
                    if (LatLonConv.VDirect(data.VLatitude, data.VLongitude,
                        data.Heading, data.Speed / 3060.0 * _timeTogoValue,
                        out double lat, out double lon, out double az) == TCoordConvStatus.OKNOERROR)
                    {
                        LatLon2XY(lat, lon, out double scrX2, out double scrY2);
                        GL.Color4(1.0, 1.0, 0.0, 1.0);
                        GL.Begin(PrimitiveType.Lines);
                        GL.Vertex2(scrX, scrY);
                        GL.Vertex2(scrX2, scrY2);
                        GL.End();
                    }
                }

                // 항공기 타입에 따라 이미지 선택
                Ntds2d.DrawAirplaneImage(scrX, scrY, data.Altitude, airplaneScale * 0.5, data.Heading, imageNum, data.IsGhost, data.IsConflictRisk);


                if(AircraftManager.IsFocusedAircraft(data.HexAddr))
                {
                    LatLon2XY(data.VLatitude, data.VLongitude, out double x, out double y);
                    GL.LineWidth((float)(airplaneScale * 1.2));
                    GL.Color4(1.0f, 0.078f, 0.576f, 1.0f); // 형광 분홍색 (Deep Pink / #FF1493)
                    Ntds2d.DrawCircleOutline(x, y, 20.0, 50);
                }
                
                GL.PopAttrib();
            }

            // 최초로 항공기가 그려진 시점 기록
            if ((connectStartTime != null) && (msgReceiveTime != null))
            {
                firstAircraftDrawTime = DateTime.Now;
                Logger.Info($"[SBS] Display Time: {firstAircraftDrawTime.Value:HH:mm:ss.fff}");
                Logger.Info($"[SBS] Click Button -> Display aircraft: {(firstAircraftDrawTime.Value - connectStartTime.Value).TotalMilliseconds} ms");
                Logger.Info($"[SBS] Receive Msg -> Display aircraft: {(firstAircraftDrawTime.Value - msgReceiveTime.Value).TotalMilliseconds} ms");

                connectStartTime = null; // 이후에는 다시 초기화
                msgReceiveTime = null; // 이후에는 다시 초기화
            }
        }

        private void DrawOldTrack(Aircraft aircraft)
        {
            // 항공기 트랙 포인트 그리기
            if (aircraft.TrackPoint.Items.Count >= 1)
            {
                AircraftTrackPoint tp;
                AircraftTrackPoint prev;
                int i = 0;
                for (; i < aircraft.TrackPoint.Items.Count-1; i++)
                {
                    prev = aircraft.TrackPoint.Items[i];
                    tp = aircraft.TrackPoint.Items[i+1];

                    DrawTrack(prev, tp, tp.Altitude);
                }
                prev = aircraft.TrackPoint.Items[i];
                DrawTrack(prev, new AircraftTrackPoint(aircraft.VLatitude, aircraft.VLongitude, aircraft.Altitude, 0), aircraft.TrackPoint.Items[i].Altitude);
            }
        }

        private void DrawTrack(AircraftTrackPoint prev, AircraftTrackPoint tp, double alt)
        {
            double scrX1, scrY1, scrX2, scrY2;

            LatLon2XY(prev.Latitude, prev.Longitude, out scrX1, out scrY1);
            LatLon2XY(tp.Latitude, tp.Longitude, out scrX2, out scrY2);
            Ntds2d.DrawLeader(scrX1, scrY1, scrX2, scrY2, 1.0f, AltitudeToColor.GetAltitudeColorRGB(alt));

            
        }

        private void DrawTrackHook()
        {
            var hook = AircraftManager.TrackHook;
            if (!hook.Valid_CC) return;

            GL.PushAttrib(AttribMask.CurrentBit);
            if (AircraftManager.TryGet(hook.ICAO_CC, out var data))
            {
                LatLon2XY(data.VLatitude, data.VLongitude, out double x, out double y);
                GL.LineWidth((float)airplaneScale);
                GL.Color4(1.0f, 1.0f, 0.0f, 1.0f); // 노란색
                Ntds2d.DrawCircleOutline(x, y, 20.0, 50);
                DrawOldTrack(data); // 이전 트랙 포인트 그리기
                //aircraftPopup.HorizontalOffset = x + 10; // +10은 약간 오른쪽 오프셋
                //aircraftPopup.VerticalOffset = y - 20;   // 위로 살짝

                //aircraftPopup.IsOpen = true;
            }
            else
            {
                hook.Valid_CC = false;
                //aircraftPopup.IsOpen = false;
            }
            GL.PopAttrib();
        }

        private void DrawFlightRoute()
        {
            var hook = AircraftManager.TrackHook;
            if (hook.DepartureAirport == null || hook.ArrivalAirport == null) return;

            for (int i = 0; i < hook.DrawAirports.Count - 1; i++)
            {
                var deptAirport = hook.DrawAirports[i];
                var arrAirport = hook.DrawAirports[Math.Min(i + 1, hook.DrawAirports.Count - 1)];

                bool isLast = (i == hook.DrawAirports.Count - 1);

                if (double.TryParse(deptAirport["Latitude"], out double ddLat) &&
                double.TryParse(deptAirport["Longitude"], out double ddLon) &&
                double.TryParse(arrAirport["Latitude"], out double daLat) &&
                double.TryParse(arrAirport["Longitude"], out double daLon))
                {
                    // 1. 출발지점의 화면 좌표는 기존 방식으로 계산합니다.
                    LatLon2XY(ddLat, ddLon, out double dScrX, out double dScrY);

                    // 2. 도착지점의 경도를 조정할 변수를 만듭니다.
                    double adjustedDaLon = daLon;

                    // 3. 출발지와 도착지의 경도 차이를 계산합니다.
                    double lonDifference = daLon - ddLon;

                    // 4. 경도차가 180도보다 크면 '짧은 경로'로 가도록 경도를 조정합니다.
                    //    (예: 동경 170도 -> 서경 170도로 갈 때)
                    if (lonDifference > 180)
                    {
                        adjustedDaLon -= 360; // 서쪽으로 이동한 것처럼 경도를 조정
                    }
                    else if (lonDifference < -180)
                    {
                        adjustedDaLon += 360; // 동쪽으로 이동한 것처럼 경도를 조정
                    }

                    // 5. WrapLongitude()를 피하기 위해, LatLon2XY의 계산 로직을 직접 사용하여
                    //    '조정된 경도(adjustedDaLon)'로 도착지점의 화면 좌표를 계산합니다.
                    double aScrX, aScrY;
                    aScrX = (Map_v[1].X - ((Map_w[1].X - (adjustedDaLon / 360.0)) / xf));
                    aScrY = Map_v[3].Y - (Map_w[1].Y / yf) + (MathExt.Asinh(Math.Tan(daLat * Math.PI / 180.0)) / (2 * Math.PI * yf));

                    // 6. 계산된 화면 좌표로 경로를 그립니다.
                    // Ntds2d.DrawGreatCircleArc(ddLat, ddLon, daLat, daLon, 50, LatLon2XY);
                    Ntds2d.Draw2dCurve(dScrX, dScrY, aScrX, aScrY, 0.2, 30, isLast);
                }
            }
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

            airplaneScale = Math.Min((0.05 / _earthView.Eye.H), 1.5); // 스케일 계산
            //AircraftManager.UpdateAll(Map_v); // 화면에 표시할 항공기 업데이트
        }

        private void LatLon2XY(double lat, double lon, out double x, out double y)
        {
            // 경도를 -180~180 기준으로 wrap-around
            lon = WrapLongitude(lon);

            x = (Map_v[1].X - ((Map_w[1].X - (lon / 360.0)) / xf));
            y = Map_v[3].Y - (Map_w[1].Y / yf) + (MathExt.Asinh(Math.Tan(lat * Math.PI / 180.0)) / (2 * Math.PI * yf));
        }

        private double WrapLongitude(double lon)
        {
            lon = (lon + 180) % 360;
            if (lon < 0) lon += 360;
            return lon - 180;
        }

        private void SetMapCenter(out double x, out double y)
        {
            double siny;
            x = (MapCenterLon + 0.0) / 360.0;
            siny = Math.Sin((MapCenterLat * Math.PI) / 180.0);
            siny = Math.Min(Math.Max(siny, -0.9999), 0.9999);
            y = (Math.Log((1 + siny) / (1 - siny)) / (4 * Math.PI));
        }

        private void CenterMapTo(object msg)
        {
            (double lat, double lon) = ((double, double))msg;
            
            CenterMapTo(lat, lon);
        }

        public void CenterMapTo(double latitude, double longitude)
        {
            MapCenterLat = latitude;
            MapCenterLon = longitude;

            SetMapCenter(out double x, out double y);
            _earthView.Eye.X = x;
            _earthView.Eye.Y = y;

            UpdateRegion();
            glControl.InvalidateVisual(); // 화면 갱신
        }

        // ────────────────────────────────────────────────
        // 화면 → 텍스처 캡처
        private void CaptureFreezeTexture()
        {
            ReleaseFreezeTexture();

            _freezeW = (int)glControl.ActualWidth;
            _freezeH = (int)glControl.ActualHeight;
            if (_freezeW <= 0 || _freezeH <= 0) return;

            // 지도+항공기+오버레이까지 모두 렌더
            GL.Clear(ClearBufferMask.ColorBufferBit);
            _earthView.Render(_mapDisplay);
            MapManager.Instance.ClearTitleManager();
            DrawObject();

            GL.Flush();

            _isDragging = true;

            _freezeTexId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _freezeTexId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // ★ ClampToEdge 추가 (아래 두 줄)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                          _freezeW, _freezeH, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.CopyTexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 0, 0, _freezeW, _freezeH);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void CaptureWideFreezeTexture()
        {
            ReleaseFreezeTexture();

            int captureW = (int)glControl.ActualWidth * 2;
            int captureH = (int)glControl.ActualHeight * 2;

            _freezeW = captureW;
            _freezeH = captureH;
            if (_freezeW <= 0 || _freezeH <= 0) return;

            int[] oldViewport = new int[4];
            GL.GetInteger(GetPName.Viewport, oldViewport);

            GL.Viewport(0, 0, _freezeW, _freezeH);
            _earthView.Resize(_freezeW, _freezeH);

            GL.Clear(ClearBufferMask.ColorBufferBit);
            _earthView.Render(_mapDisplay);
            MapManager.Instance.ClearTitleManager();

            GL.PushAttrib(AttribMask.AllAttribBits);
            DrawObject();
            GL.PopAttrib();

            _pendingCapture = false;
            _isDragging = true;

            GL.Flush();

            _freezeTexId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _freezeTexId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // ★ ClampToEdge 추가 (아래 두 줄)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _freezeW, _freezeH, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.CopyTexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 0, 0, _freezeW, _freezeH);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            GL.Viewport(oldViewport[0], oldViewport[1], oldViewport[2], oldViewport[3]);
            _earthView.Resize((int)glControl.ActualWidth, (int)glControl.ActualHeight);
        }

        private void ReleaseFreezeTexture()
        {
            if (_freezeTexId != -1)
            {
                GL.DeleteTexture(_freezeTexId);
                _freezeTexId = -1;
            }
        }

        // 텍스처를 화면에 덮어쓰기
        private void DrawFreezeTexture()
        {
            int viewW = (int)glControl.ActualWidth;
            int viewH = (int)glControl.ActualHeight;

            int dx = _dragOffsetX;
            int dy = _dragOffsetY;

            // X축: 반대로 계산
            int srcX = Math.Max(0, -dx);
            int dstX = Math.Max(0, dx);

            // Y축: 기존대로
            int srcY = Math.Max(0, dy);
            int dstY = Math.Max(0, -dy);

            int copyW = Math.Min(_freezeW - srcX, viewW - dstX);
            int copyH = Math.Min(_freezeH - srcY, viewH - dstY);

            // 1. OpenGL 상태 명확화
            GL.MatrixMode(MatrixMode.Projection);
            GlUtil.Projection2D(0, 0, viewW, viewH);
            GL.ClearColor(_mapDisplay ? 0f : BG_INTENSITY, _mapDisplay ? 0f : BG_INTENSITY, _mapDisplay ? 0f : BG_INTENSITY, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // 2. 실시간 지도 전체 그리기
            _earthView.Render(_mapDisplay);

            if (copyW > 0 && copyH > 0)
            {
                GL.Enable(EnableCap.Texture2D);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.BindTexture(TextureTarget.Texture2D, _freezeTexId);

                float texX0 = (float)srcX / _freezeW;
                float texY0 = (float)srcY / _freezeH;
                float texX1 = (float)(srcX + copyW) / _freezeW;
                float texY1 = (float)(srcY + copyH) / _freezeH;

                GL.Begin(PrimitiveType.Quads);
                GL.TexCoord2(texX0, texY0); GL.Vertex2(dstX, dstY);
                GL.TexCoord2(texX1, texY0); GL.Vertex2(dstX + copyW, dstY);
                GL.TexCoord2(texX1, texY1); GL.Vertex2(dstX + copyW, dstY + copyH);
                GL.TexCoord2(texX0, texY1); GL.Vertex2(dstX, dstY + copyH);
                GL.End();

                GL.Disable(EnableCap.Texture2D);
                GL.Disable(EnableCap.Blend);
            }
        }
    }
}
