using ADS_B_Display.Models;
using ADS_B_Display.Utils;
using MahApps.Metro.Controls;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace ADS_B_Display.Views
{
    public partial class CPAConflictDialog : MetroWindow, INotifyPropertyChanged
    {
        public CPAConflictInfo SelectedConflict { get; private set; }

        public CPAConflictDialog()
        {
            InitializeComponent();

            DataContext = this; // 🔑 ViewModel로 자기 자신을 바인딩

            LoadConflictList();

            AircraftManager.CPAConflicts.CollectionChanged += OnConflictChanged;
        }
        private void LoadConflictList()
        {
            var list = AircraftManager.CPAConflicts
                .OrderBy(c => c.TCPA_Seconds)
                .Select((c, i) => new CPAConflictDisplayModel
                {
                    Index = i + 1,
                    HexAddr1 = c.HexAddr1,
                    HexAddr2 = c.HexAddr2,
                    TCPA_Seconds = c.TCPA_Seconds,
                    CPADistance_NM = c.CPADistance_NM,
                    Vertical_ft = c.Vertical_ft,
                    AreaName1 = c.AreaName1,
                    Raw = c
                }).ToList();

            CpaDataGrid.ItemsSource = list;

            if (list.Count > 0)
            {
                var first = list[0];
                SelectedConflictText = $"# {first.HexAddr1} vs {first.HexAddr2} | TCPA : {first.TCPA_Seconds:F2} sec | CPA_Dist: {first.CPADistance_NM:F2} NM | Vertical_Dist: {first.Vertical_ft:F0} ft";
                CpaDataGrid.SelectedItem = first;
            }
        }

        private void OnConflictChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                // UI 스레드가 아니면 BeginInvoke로 비동기 안전하게 넘김
                Dispatcher.BeginInvoke(new Action(() => OnConflictChanged(sender, e)));
                return;
            }

            // UI 스레드에서 실행 중이므로 안전하게 UI 접근 가능
            var snapshot = AircraftManager.GetCPAConflicts(); // Snapshot으로 동시성 문제 방지
            CpaDataGrid.ItemsSource = snapshot
                .OrderBy(c => c.CPADistance_NM)
                .Select((c, i) => new CPAConflictDisplayModel
                {
                    Index = i + 1,
                    HexAddr1 = c.HexAddr1,
                    HexAddr2 = c.HexAddr2,
                    TCPA_Seconds = c.TCPA_Seconds,
                    CPADistance_NM = c.CPADistance_NM,
                    Vertical_ft = c.Vertical_ft,
                    AreaName1 = c.AreaName1,
                    Raw = c
                }).ToList();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private string _selectedConflictText;
        public string SelectedConflictText
        {
            get => _selectedConflictText;
            set
            {
                _selectedConflictText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedConflictText)));
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            AircraftManager.CPAConflicts.CollectionChanged -= OnConflictChanged;
            AircraftManager.SetFocusedAircraft(null, null);
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // DialogResult = false; ❌ 사용 금지
            Dispatcher.Invoke(() => this.Close());
        }
        private void CpaDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

            if (CpaDataGrid.SelectedItem is CPAConflictDisplayModel selected)
            {
                var hex1 = selected.HexAddr1; // 또는 HexAddr2
                var hex2 = selected.HexAddr2; // 또는 HexAddr2
                double lat = selected.Raw.Lat1;
                double lon = selected.Raw.Lon1;
                SelectedConflictText = $"# {selected.HexAddr1} vs {selected.HexAddr2} | TCPA : {selected.TCPA_Seconds:F2} sec | CPA_Dist: {selected.CPADistance_NM:F2} NM | Vertical_Dist: {selected.Vertical_ft:F0} ft";
                AircraftManager.SetFocusedAircraft(hex1, hex2);
                EventBus.Publish(EventIds.EvtCenterMapTo, (lat, lon));
                
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

            public double Vertical_ft { get; set; }

            public string AreaName1 { get; set; } // 추가
            public string AreaName2 { get; set; } // 추가
            public CPAConflictInfo Raw { get; set; } // 내부 데이터 참조용
        }
    }
}