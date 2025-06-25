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
    /// AreaRegisterPopup.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AreaRegisterPopup : Window
    {
        public AreaRegisterPopup()
        {
            InitializeComponent();
        }

        public string AreaName { get; set; }

        public Color AreaColor { get; set; }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            AreaName = tbAreaName.Text;
            AreaColor = Colors.Red;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
