﻿using System;
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

namespace ADS_B_Display.Views
{
    /// <summary>
    /// AircraftControlView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AircraftControlView : UserControl, IDisposable
    {
        public AircraftControlView()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
            (DataContext as IDisposable)?.Dispose();
        }

        private void dgArea_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ((AircraftControlViewModel)DataContext).MouseDoubleClick();
        }
    }
}
