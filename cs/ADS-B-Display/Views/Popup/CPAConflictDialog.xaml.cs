using ADS_B_Display.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;

namespace ADS_B_Display.Views
{
    public partial class CPAConflictDialog : Window
    {
        public CPAConflictInfo SelectedConflict { get; private set; }


        public CPAConflictDialog()
        {
            InitializeComponent();

            LoadConflictList();

            AircraftManager.CPAConflicts.CollectionChanged += OnConflictChanged;
        }
        private void LoadConflictList()
        {
            CpaDataGrid.ItemsSource = AircraftManager.CPAConflicts
                .OrderBy(c => c.TCPA_Seconds) // ✅ TCPA 기준 정렬
                .Select((c, i) => new CPAConflictDisplayModel
                {
                    Index = i + 1,
                    HexAddr1 = c.HexAddr1,
                    HexAddr2 = c.HexAddr2,
                    TCPA_Seconds = c.TCPA_Seconds,
                    CPADistance_NM = c.CPADistance_NM,
                    Raw = c
                }).ToList();
        }

        private void OnConflictChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                CpaDataGrid.ItemsSource = AircraftManager.CPAConflicts
                    .OrderBy(c => c.TCPA_Seconds) // ✅ TCPA 기준 정렬
                    .Select((c, i) => new CPAConflictDisplayModel
                    {
                        Index = i + 1,
                        HexAddr1 = c.HexAddr1,
                        HexAddr2 = c.HexAddr2,
                        TCPA_Seconds = c.TCPA_Seconds,
                        CPADistance_NM = c.CPADistance_NM,
                        Raw = c
                    }).ToList();
            });
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            AircraftManager.CPAConflicts.CollectionChanged -= OnConflictChanged;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (CpaDataGrid.SelectedItem is CPAConflictDisplayModel selected)
            {
                SelectedConflict = selected.Raw;
                DialogResult = true;
            }
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    // Helper class for grid binding
    public class CPAConflictDisplayModel
    {
        public int Index { get; set; }
        public string HexAddr1 { get; set; }
        public string HexAddr2 { get; set; }
        public double TCPA_Seconds { get; set; }
        public double CPADistance_NM { get; set; }

        public CPAConflictInfo Raw { get; set; } // 내부 데이터 참조용
    }
}
