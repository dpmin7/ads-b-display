using ADS_B_Display.Map;
using ADS_B_Display.Map.MapSrc;
using ADS_B_Display.Models;
using ADS_B_Display.Views;
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
            AircraftControlPanel.DataContext = new AircraftControlViewModel();

            // read aircraft data from file if exists
            var aircraftDir = $"{Directory.GetCurrentDirectory()}\\AircraftDB";
            //AircraftDB.Init(aircraftDir);
        }



        private void TimeToGoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 예: 초 단위로 e.NewValue를 받아와 “hh:mm:ss.fff” 형태로 변환 후 TimeToGoValueText.Text에 대입
            TimeSpan t = TimeSpan.FromSeconds(e.NewValue);
            //TimeToGoValueText.Text = string.Format("{0:00}:{1:00}:{2:00}.{3:000}",
            //                                       t.Hours,
            //                                       t.Minutes,
            //                                       t.Seconds,
            //                                       t.Milliseconds);
        }


        // Playback 속도
        private void PlaybackSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //PlaybackSpeedValueText.Text = "PlayBack Speed: " + e.NewValue + "x";
            //if (_sbsWorker != null) {
            //    _sbsWorker.setPlayBackSpeed((int)e.NewValue);
            //}
        }
        


        private void UseSbsLocal_Click(object sender, RoutedEventArgs e)
        {
            //SbsConnectTextBox.Text = "128.237.96.41";
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


        private double _x = 1.0f, _y = 0.5f, _z = 0.0f;



        private double airplaneScale;










        private bool _isLoaded = false;
        private int _MouseLeftDownX;
        private int _MouseLeftDownY;

        private void UseSbsRemote_Click(object sender, RoutedEventArgs e)
        {

        }

        private int _MouseDownMask;

        private const int LEFT_MOUSE_DOWN = 1; // 마우스 왼쪽 버튼 클릭 상태 플래그
        private const int RIGHT_MOUSE_DOWN = 2; // 마우스 오른쪽 버튼 클릭 상태 플래그
        private const int MIDDLE_MOUSE_DOWN = 4; // 마우스 가운데 버튼 클릭 상태 플래그

        private Area _areaTemp; // 폴리곤 영역 임시 저장용. 나중에 하자
        private StreamReader _playbackSbsStream;



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

    }

    public class AreaOfInterest
    {
        public string AreaName { get; set; }
        public SolidColorBrush AreaColorBrush { get; set; }
    }
}
