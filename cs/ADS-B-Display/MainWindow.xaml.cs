using ADS_B_Display.Views;
using MahApps.Metro.Controls;
using System;
using System.IO;
using System.Windows;
//using System.Windows.Shapes;

namespace ADS_B_Display
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
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
            //SbsConnectTextBox.Text = "128.237.96.41"; // "data.adsbhub.org"
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            (airScreenPanelView as IDisposable)?.Dispose();
            (AircraftControlPanel as IDisposable)?.Dispose();
        }
    }
}
