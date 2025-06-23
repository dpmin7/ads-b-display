using ADS_B_Display.Views;
using System.IO;
using System.Windows;
//using System.Windows.Shapes;

namespace ADS_B_Display
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
#if false // PingEcho 테스트용 코드 (필요시 활성화)    
        // Ping 관련 필드
        private PingEcho pingEcho = new PingEcho();
#endif

        public MainWindow()
        {
            InitializeComponent();
            AircraftControlPanel.DataContext = new AircraftControlViewModel();

            // read aircraft data from file if exists
            var aircraftDir = $"{Directory.GetCurrentDirectory()}\\AircraftDB";
            //AircraftDB.Init(aircraftDir);
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

        private void UseSbsRemote_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
