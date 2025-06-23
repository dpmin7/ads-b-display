using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ADS_B_Display.Views.UserControls
{
    public enum ConnectStatus { Connect, Error, Disconnect }
    /// <summary>
    /// ConnectionSignal.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ConnectionSignal : UserControl
    {
        public ConnectionSignal()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ConnectionStatusProperty = DependencyProperty.Register(
            nameof(ConnectionStatus), typeof(ConnectStatus), typeof(ConnectionSignal),
            new PropertyMetadata(ConnectStatus.Disconnect, OnConnectionStatusChanged));

        public ConnectStatus ConnectionStatus {
            get => (ConnectStatus)GetValue(ConnectionStatusProperty);
            set => SetValue(ConnectionStatusProperty, value);
        }

        private static void OnConnectionStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ConnectionSignal control) {
                control.UpdateColor();
            }
        }

        private void UpdateColor()
        {
            switch (ConnectionStatus) {
                case ConnectStatus.Connect:
                    StatusLight.Fill = Brushes.LimeGreen;
                    break;
                case ConnectStatus.Error:
                    StatusLight.Fill = Brushes.Gold;
                    break;
                default:
                    StatusLight.Fill = Brushes.Gray;
                    break;
            }
        }
    }
}
