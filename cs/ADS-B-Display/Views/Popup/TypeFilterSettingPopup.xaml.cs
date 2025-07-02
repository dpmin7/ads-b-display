using MahApps.Metro.Controls;
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
using System.Windows.Shapes;

namespace ADS_B_Display.Views.Popup
{
    /// <summary>
    /// Interaction logic for TypeFilterSettingPopup.xaml
    /// </summary>
    public partial class TypeFilterSettingPopup : MetroWindow
    {
        public TypeFilterSettingPopup()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {  
            Close();
        }
    }
}
