using ADS_B_Display.Map;
using ADS_B_Display.Map.MapSrc;
using ADS_B_Display.Models;
using ADS_B_Display.Models.Settings;
using ADS_B_Display.Utils;
using Microsoft.SqlServer.Server;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Timers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ADS_B_Display.Views
{
    /// <summary>
    /// AirScreenPanelView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AirScreenPanelView : UserControl, IDisposable
    {
        private int _MouseLeftDownX;
        private int _MouseLeftDownY;
        private int _MouseDownMask;

        private const int LEFT_MOUSE_DOWN = 1; // 마우스 왼쪽 버튼 클릭 상태 플래그
        private const int RIGHT_MOUSE_DOWN = 2; // 마우스 오른쪽 버튼 클릭 상태 플래그
        private const int MIDDLE_MOUSE_DOWN = 4; // 마우스 가운데 버튼 클릭 상태 플래그

        // Map 관련 필드
        double Mw1, Mw2, Mh1, Mh2, xf, yf;
        public Vector3d[] Map_v = new Vector3d[4];
        public Vector3d[] Map_p = new Vector3d[4];
        public Vector2d[] Map_w = new Vector2d[2];
        double MapCenterLat, MapCenterLon;
        private TrackHookStruct _trackHook = new TrackHookStruct();

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


        public AirScreenPanelView()
        {
            InitializeComponent();
            EventBus.Observe(EventIds.EvtControlSettingChanged).Subscribe(msg => UpdateTimeToGo(msg));

            var settings = new GLWpfControlSettings()
            {
                MajorVersion = 2, // OpenGL Major Version
                MinorVersion = 1, // OpenGL Minor Version
                RenderContinuously = false
            };
            glControl.Start(settings);

            MapManager.Instance.RegisterLoadMapCallback(MapLoaded);
            MapManager.Instance.LoadMap(TileServerType.GoogleMaps);
            //LoadMap(TileServerType.GoogleMaps);

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
            airplaneScale = Math.Min((0.05 / _earthView.Eye.H), 1.5); // 스케일 계산

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
        }

        public void Dispose()
        {
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

        private void MapLoaded(MasterLayer masterLayer)
        {
            _earthView = new FlatEarthView(masterLayer);
            _earthView.Resize((int)glControl.ActualWidth, (int)glControl.ActualHeight);

            UpdateRegion();
        }

        private void glControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 마우스 좌표 구하기 (정수형으로 변환)
            int x = (int)e.GetPosition(glControl).X;
            int y = (int)e.GetPosition(glControl).Y;
            // 마우스 왼쪽 버튼 클릭 확인
            if (e.ChangedButton == MouseButton.Left)
            {
                // Ctrl 키가 눌렸는지 확인
                if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                {
                    // Ctrl + 왼쪽 클릭 시 동작 (필요시 구현)
                }
                else
                {
                    // 필요한 전역 변수에 값 저장 (예시)
                    _MouseLeftDownX = x;
                    _MouseLeftDownY = y;
                    _MouseDownMask |= LEFT_MOUSE_DOWN;

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
                bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
                HookTrack(x, y, ctrl);
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
            }
        }

        private void glControl_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            // 마우스 왼쪽 버튼이 떼어졌을 때만 처리
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                _MouseDownMask &= ~LEFT_MOUSE_DOWN;
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
        }

        private double airplaneScale;
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                _earthView.SingleMovement(EarthView.NAV_ZOOM_IN);
            else _earthView.SingleMovement(EarthView.NAV_ZOOM_OUT);
            airplaneScale = Math.Min((0.05 / _earthView.Eye.H), 1.5); // 스케일 계산

            UpdateRegion(); // 현재 지역 업데이트
            glControl.InvalidateVisual(); // 마우스 휠 이벤트 후 강제 갱신
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

        private void glControl_Render(TimeSpan obj)
        {
            if (!_isLoaded)
                return;

            if (_mapDisplay)
                GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f); // 검은색 배경
            else
                GL.ClearColor(BG_INTENSITY, BG_INTENSITY, BG_INTENSITY, 0.0f); // 배경색 강도에 따라 설정

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit); // 화면 지우기

            _earthView.Animate(); // 애니메이션 업데이트
            _earthView.Render(_mapDisplay); // 지도 렌더링
            MapManager.Instance.ClearTitleManager(); // 타일 매니저 정리

            DrawObject(); // OpenGL 객체 그리기
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
                    _trackHook.Valid_CC = true;
                    _trackHook.ICAO_CC = selectedAircraft.ICAO;
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

            if (_trackHook.Valid_CC)
                glControl.InvalidateVisual();

            if (!cpaHook)
                PublishHookInfo(_trackHook);
        }

        private bool _prevValid_CC = false;
        private uint _prevICAO_CC = 0;
        private void PublishHookInfo(TrackHookStruct trackHook)
        {
            if (_prevICAO_CC == trackHook.ICAO_CC &&
                _prevValid_CC == trackHook.Valid_CC)
            { // 같은 놈 같은 상태면 업데이트 하지 말자.
                return;
            }
            _prevValid_CC = trackHook.Valid_CC;
            _prevICAO_CC = trackHook.ICAO_CC;
            EventBus.Publish(EventIds.EvtAircraftHooked, trackHook);
        }

        private void DrawObject()
        {
            int viewableAircraft = 0;
            double scrX, scrY;
            GL.Enable(EnableCap.LineSmooth);// 선 부드럽게
            GL.Enable(EnableCap.PointSmooth); // 포인트 부드럽게
            GL.Enable(EnableCap.Blend); // 알파 블렌딩 활성화
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); // 알파 블렌딩 함수 설정
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest); // 선 부드럽게 처리
            GL.LineWidth(3.0f); // 선 너비 설정
            GL.PointSize(4.0f); // 포인트 크기 설정
            GL.Color4(1.0f, 1.0f, 1.0f, 1.0f); // 기본 색상 설정 (흰색)

            LatLon2XY(MapCenterLat, MapCenterLon, out scrX, out scrY);

            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex2(scrX - 20.0, scrY);
            GL.Vertex2(scrX + 20.0, scrY);
            GL.End();

            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex2(scrX, scrY - 20.0);
            GL.Vertex2(scrX, scrY + 20.0);
            GL.End();


            var tempArea = AreaManager.TempArea;
            GL.Color4(1f, 1, 1f, 1f);
            if (tempArea != null && tempArea.NumPoints > 0)
            {
                GL.PointSize(3f);
                var adj = new Vector2d[tempArea.NumPoints];
                for (int i = 0; i < tempArea.NumPoints; i++)
                {
                    LatLon2XY(tempArea.Points[i].Y, tempArea.Points[i].X,
                               out adj[i].X, out adj[i].Y);
                }

                GL.Begin(PrimitiveType.Points);
                for (int i = 0; i < tempArea.NumPoints; i++)
                    GL.Vertex2(adj[i].X, adj[i].Y);
                GL.End();

                GL.Begin(PrimitiveType.LineStrip);
                for (int i = 0; i < tempArea.NumPoints; i++)
                    GL.Vertex2(adj[i].X, adj[i].Y);
                GL.End();
            }

            // 저장된 영역들(Areas) 그리기
            foreach (var area in AreaManager.Areas)
            {
                // 색상 변환
                var mc = area.Color;

                // 영역 내부 채우기 (삼각형)
                // 삼각형 리스트 반복
                GL.Enable(EnableCap.PolygonOffsetFill);
                GL.PolygonOffset(1.0f, 1.0f); // 한 번만 설정

                GL.Color4(mc.R / 255f, mc.G / 255f, mc.B / 255f, 0.4f);
                GL.Begin(PrimitiveType.Triangles);
                foreach (var tri in area.Triangles)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var idx = tri[k];
                        if (idx >= area.NumPoints) continue;
                        LatLon2XY(area.Points[(int)idx].Y, area.Points[(int)idx].X, out scrX, out scrY);
                        GL.Vertex2(scrX, scrY);
                    }
                }
                GL.End();
                GL.Disable(EnableCap.PolygonOffsetFill); // 정리

                if (area.Selected)
                {
                    GL.LineWidth(4f);
                    GL.PushAttrib(AttribMask.LineBit);
                    GL.LineStipple(3, 0xAAAA);
                }

                GL.Color4(mc.R / 255f, mc.G / 255f, mc.B / 255f, 1f);
                GL.Begin(PrimitiveType.LineLoop);
                for (int j = 0; j < area.NumPoints; j++)
                {
                    LatLon2XY(area.Points[j].Y, area.Points[j].X,
                               out scrX, out scrY);
                    GL.Vertex2(scrX, scrY);
                }
                GL.End();

                if (area.Selected)
                {
                    GL.PopAttrib();
                    GL.LineWidth(2f);
                }
            }

            // 공항 정보 표시
            List<Dictionary<string, string>> airportsInfo = AirportDB.GetAirPortsInfo();
            HashSet<string> uniqueAirports = AirportDB.GetUniqueAirportCodes();

            if (airportsInfo != null)
            {
                foreach (var row in airportsInfo)
                {
                    string icao = row["ICAO"];
                    string latitude = row["Latitude"];
                    string longitude = row["Longitude"];

                    if (uniqueAirports.Contains(icao) && double.TryParse(latitude, out double lat) && double.TryParse(longitude, out double lon))
                    {
                        if (lat > 85.0511 || lat < -85.0511)
                            continue;

                        LatLon2XY(lat, lon, out double cLat, out double cLon);

                        Ntds2d.DrawAirportVBO(cLat, cLon, airplaneScale * 0.6);
                    }
                }
            }


            // 항공기 정보 그리기
            var aircraftTable = AircraftManager.GetAll();
            foreach (var data in aircraftTable)
            {
                if (!data.HaveLatLon) continue;

               //hyunjae - 임시로 다각형이 있으면 다격형 내에 항공기만 전시하도록 하는 코드
                if(AreaManager.Areas.Count > 0)
                {
                    bool isInsideAnyArea = false;
                    foreach (var area in AreaManager.Areas)
                    {
                        if (PointPolygonFilter.IsPointInArea(data.Latitude, data.Longitude, area.Points.ToArray()))
                        {
                            isInsideAnyArea = true;
                            break;
                        }
                    }
                    if (!isInsideAnyArea) continue;
                }

                viewableAircraft++;
                GL.Color4(1f, 1f, 1f, 1f);
                LatLon2XY(data.VLatitude, data.VLongitude, out scrX, out scrY);

                if (data.HaveSpeedAndHeading)
                    GL.Color4(1f, 0f, 1f, 1f);
                else
                {
                    data.Heading = 0;
                    GL.Color4(1f, 0f, 0f, 1f);
                }

                Ntds2d.DrawAirplaneImage(scrX, scrY, data.Altitude, airplaneScale * 0.5, data.Heading, data.SpriteImage, data.IsGhost);
                //glControl.Draw2DText(data.HexAddr, scrX + 10, scrY - 10, System.Drawing.Color.Pink);
                // TODO: Draw2DText 구현 필요

                if ((data.HaveSpeedAndHeading) && (_useTimeToGo))
                {
                    double lat, lon, az;
                    if (LatLonConv.VDirect(data.VLatitude, data.VLongitude,
                                data.Heading, data.Speed / 3060.0 * _timeTogoValue, out lat, out lon, out az) == TCoordConvStatus.OKNOERROR) {
                        double scrX2, scrY2;
                        LatLon2XY(lat, lon, out scrX2, out scrY2);
                        GL.Color4(1.0, 1.0, 0.0, 1.0);
                        GL.Begin(PrimitiveType.Lines);
                        GL.Vertex2(scrX, scrY);
                        GL.Vertex2(scrX2, scrY2);
                        GL.End();
                    }
                }
            }

            // TrackHook 정보 그리기
            if (_trackHook.Valid_CC)
            {
                if (AircraftManager.TryGet(_trackHook.ICAO_CC, out var data))
                {
                    LatLon2XY(data.VLatitude, data.VLongitude, out scrX, out scrY);
                    Ntds2d.DrawTrackHook(scrX, scrY, airplaneScale * 0.5);
                }
                else
                {
                    _trackHook.Valid_CC = false;
                }
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
    }
}
