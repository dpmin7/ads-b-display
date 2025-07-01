using ADS_B_Display.Models;
using ADS_B_Display.Models.Connector;
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
    /// BigQueryListPopup.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class BigQueryListPopup : MetroWindow
    {
        public BigQueryListPopup(List<BigQueryListItem> items)
        {
            InitializeComponent();
            bigQueryList.ItemsSource = items;
        }

        public BigQueryListItem SelectedItem
        {
            get
            {
                return (BigQueryListItem)bigQueryList.SelectedItem;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
