using ADS_B_Display.Map;
using ADS_B_Display.Map.MapSrc;
using ADS_B_Display.Models;
using ADS_B_Display.Models.Settings;
using ADS_B_Display.Utils;
using ADS_B_Display.Views.Popup;
using ADS_B_Display.Views.UserControls;
using Microsoft.Win32;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ADS_B_Display.Views
{
    internal class AircraftControlViewModel : NotifyPropertyChangedBase, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private SbsWorker _sbsWorker = null;
        private SbsWorker _rawWorker = null;

        //Area Insert Module
        private bool _canCompleteOrCancel = false;
        public ObservableCollection<Area> AreaList { get; }
        private Area _selectedArea;
        private DispatcherTimer _timer = null;

        private PingEcho pingEcho = new PingEcho();
        IDbWriterReader _db;

        private string _tempAreaName { get; set; }

        private Color AreaColor { get; set; }

        private string _selectedBigQueryTable = null;

        public AircraftControlViewModel()
        {
            _sbsWorker = new SbsWorker(AircraftManager.ReceiveSBSMessage);
            _rawWorker = new SbsWorker(AircraftManager.ReceiveRawMessage);

            RegisterEvents();

            ControlSettings = Setting.Instance.ControlSettings;
            if (ControlSettings == null)
            {
                ControlSettings = new ControlSettings();
                Setting.Instance.ControlSettings = ControlSettings;
            }

            OnChangeSetting();

            if (Enum.TryParse(ControlSettings.MapProvider, true, out TileServerType res))
                SelectedTileServer = res;
            else
                SelectedTileServer = TileServerType.GoogleMaps; // 기본값 설정

            AreaManager.LoadArea(ControlSettings.AreaList.Select(areaConfig => Area.AreaConfigToArea(areaConfig)).ToList());
                                                         

            Cmd_RawConnect = new DelegateCommand(RawConnect, CanRawConnect);
            Cmd_RawDisconnect = new DelegateCommand(RawDisconnect, CanRawDisconnect);
            Cmd_SbsConnect = new DelegateCommand(SbsConnect, CanSbsConnect);
            Cmd_SbsDisconnect = new DelegateCommand(SbsDisconnect, CanSbsDisconnect);

            Cmd_RawRecord = new DelegateCommand(RawRecord, CanRawRecord);
            Cmd_RawRecordStop = new DelegateCommand(RawRecordStop, CanRawRecordStop);

            Cmd_RawPlay = new DelegateCommand(RawPlay, CanRawPlay);
            Cmd_RawPlayStop = new DelegateCommand(RawPlayStop, CanRawPlayStop);

            Cmd_SbsRecord = new DelegateCommand(SbsRecord, CanSbsRecord);
            Cmd_SbsRecordStop = new DelegateCommand(SbsRecordStop, CanSbsRecordStop);

            Cmd_SbsPlay = new DelegateCommand(SbsPlay, CanSbsPlay);
            Cmd_SbsPlayStop = new DelegateCommand(SbsPlayStop, CanSbsPlayStop);

            Cmd_Purge = new DelegateCommand(Purge);

            Cmd_Analytics = new DelegateCommand(Analytics, CanAnalytics);

            InsertCommand = new DelegateCommand(InsertArea, CanInsertArea);
            CompleteCommand = new DelegateCommand(CompleteArea, CanCompleteArea);
            CancelCommand = new DelegateCommand(CancelArea, CanCancelArea);
            DeleteCommand = new DelegateCommand(DeleteArea, CanDeleteArea);
            MonitorCommand = new DelegateCommand(MonitorArea, CanMonitorArea);

            AreaList = new ObservableCollection<Area>(AreaManager.Areas);
            Cmd_ShowCpaDialog = new DelegateCommand(ShowCpaDialog);

            _timer = new DispatcherTimer(DispatcherPriority.Background) {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += (s, e) => {
                NumOfAircraft = AircraftManager.Count();
                ViewableAircraft = AircraftManager.GetAll().Count(a => a.Viewable);
                SystemTime = DateTime.Now;
                UpdateHookedAircraft();
            };

            _timer.Start();
        }

        private bool CanAnalytics(object obj)
        {
            if (Aircraft == null || ControlSettings.UseBigQuery == false)
                return false;

            return true;
        }

        private void Analytics(object obj)
        {
            if (Aircraft == null)
                return;
            var analyzer = new ADS_B_Display.Models.FlightAnalytics.FlightAnalytics();
            analyzer.AnalyzeFlightProfile(Aircraft.HexAddr, _selectedBigQueryTable);
        }

        private void ShowCpaDialog(object obj)
        {
            var dialog = new CPAConflictDialog();
            dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                var selected = dialog.SelectedConflict;
                double lat = (selected.Lat1 + selected.Lat2) / 2;
                double lon = (selected.Lon1 + selected.Lon2) / 2;

                //airScreenViewControl.CenterMapTo(centerLat, centerLon);
                //var screenView = this.AirScreenViewControl;
                //screenView.CenterMapTo(lat, lon);
            }
        }

        private bool _isRawRecording = false;
        private bool _isRawPalying = false;
        private bool _isSbsRecording = false;
        private bool _isSbsPalying = false;

        private IDisposable _mouseMoveSubscription;

        private bool CanRawRecord(object obj) => true;//RawConnectStatus == ConnectStatus.Connect; // connect 시에 _isRecord, isPlaying, isPlayStopped 초기화
        private bool CanRawRecordStop(object obj) => true;//RawConnectStatus == ConnectStatus.Connect; // connect 시에 _isRecord, isPlaying, isPlayStopped 초기화
        private bool CanRawPlay(object obj) => true;//RawConnectStatus == ConnectStatus.Disconnect && ;
        private bool CanRawPlayStop(object obj) => true;// !_isRawRecording;
        private bool CanSbsRecord(object obj) => true;//!_isRawRecording;
        private bool CanSbsRecordStop(object obj) => true;//!_isRawRecording;
        private bool CanSbsPlay(object obj) => true;//!_isRawRecording;
        private bool CanSbsPlayStop(object obj) => true;//!_isRawRecording;

        public Area SelectedArea
        {
            get => _selectedArea;
            set
            {
                _selectedArea = value;
                OnPropertyChanged(); // INotifyPropertyChanged 구현 시
            }
        }
        public void RefreshAreaList()
        {
            AreaList.Clear();
            foreach (var area in AreaManager.Areas)
                AreaList.Add(area);
        }

        private void InsertArea(object obj)
        {
            AreaManager.IsInsertMode = true;
            _canCompleteOrCancel = true;

            ((DelegateCommand)InsertCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)CompleteCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)CancelCommand).RaiseCanExecuteChanged();

            //AreaRegisterPopup popup = new AreaRegisterPopup();
            //popup.Owner = Application.Current.MainWindow;
            //popup.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            //var res = popup.ShowDialog();
            //if (res == true)
            //{
            //    var name = popup.AreaName;
            //    var color = popup.AreaColor;
            //    AreaManager.FinalizeTempAreaIfReady(name, color);
            //}
        }

        private bool CanInsertArea(object obj) => !_canCompleteOrCancel;

        private void CompleteArea(object obj)
        {
            int result = AreaManager.Orientation2DPolygon(AreaManager.TempArea);
            if (result == 0)
            {
                MessageBox.Show("Degenerate Polygon");
                AreaManager.ResetTempArea();
                CancelArea(null);
                return;
            }
            if (TrianglePoly.CheckComplex(AreaManager.TempArea.Points.ToArray(), AreaManager.TempArea.NumPoints))
            {
                MessageBox.Show("Polygon is Complex");
                AreaManager.ResetTempArea();
                CancelArea(null);
                return;
            }
            if (result == TrianglePoly.CLOCKWISE)
            {
                var pointsArray = AreaManager.TempArea.Points.ToArray();
                TrianglePoly.ReversePoints(pointsArray, pointsArray.Length);
                AreaManager.TempArea.Points = new List<Vector3d>(pointsArray);
            }

            AreaRegisterPopup popup = new AreaRegisterPopup();
            popup.Owner = Application.Current.MainWindow;
            popup.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var res = popup.ShowDialog();
            if (res == true)
            {
                var name = popup.AreaName;
                var color = popup.AreaColor;
                AreaManager.FinalizeTempAreaIfReady(name, color);
            }
            RefreshAreaList();
            AreaManager.ResetTempArea();

            AreaManager.IsInsertMode = false;
            _canCompleteOrCancel = false;
        }
        private bool CanCompleteArea(object obj) => _canCompleteOrCancel;

        private void CancelArea(object obj)
        {
            AreaManager.IsInsertMode = false;
            _canCompleteOrCancel = false;
            AreaManager.ResetTempArea();
        }

        private bool CanCancelArea(object obj) => _canCompleteOrCancel;

        private void DeleteArea(object obj)
        {
            if (SelectedArea != null)
            {
                AreaManager.RemoveArea(SelectedArea);
                AreaList.Remove(SelectedArea);
                SelectedArea = null; // 선택 초기화
            }
        }

        private bool CanMonitorArea(object obj) => true;


        private void MonitorArea(object obj)
        {
            AreaMonitorPopup monitor = new AreaMonitorPopup();
            monitor.Show();
        }

        private bool CanDeleteArea(object obj) => SelectedArea != null;

        private bool _isInsertMode;
        public bool IsInsertMode
        {
            get => _isInsertMode;
            set => SetProperty(ref _isInsertMode, value);
        }


        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;
            _mouseMoveSubscription?.Dispose();
            _mouseMoveSubscription = null;

            // 항공기/영역 데이터 정리
            AircraftManager.PurgeAll();
            // AreaManager.ClearAll(); // 필요시

            ControlSettings.MapProvider = SelectedTileServer.ToString();
            Setting.Instance.ControlSettings = ControlSettings;

            Setting.Instance.ControlSettings.AreaList = AreaManager.Areas.Select(area => Setting.AreaToAreaConfig(area)).ToList();
        }

        private void RawRecord(object obj)
        {
            if (RawConnectStatus != ConnectStatus.Connect)
                return;

            // 2) 기록 중이 아니면, SaveFileDialog 띄워서 파일 경로 선택
            var dlg = new SaveFileDialog {
                Title = "Raw Record file save",
                Filter = "Raw Log (*.raw)|*.raw|모든 파일 (*.*)|*.*",
                FileName = $"RawLog_{DateTime.Now:yyyyMMdd_HHmmss}.raw", // 기본 파일명 예시
                DefaultExt = ".raw"
            };

            bool? result = dlg.ShowDialog();
            if (result != true) {
                // 사용자가 취소했으면 아무 동작 없이 종료
                return;
            }

            string path = dlg.FileName;
            try {
                _rawWorker.RecordOn(path, false);
            } catch (Exception ex) {
                MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _rawWorker.RecordOff();
                return;
            }

            // 4) 녹화를 시작했다는 안내 메시지 (선택 사항)
            MessageBox.Show($"Start Raw record:\n{path}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            _isRawRecording = true;
        }

        private void RawRecordStop(object obj)
        {
            if (_isRawRecording == false)
                return;

            try {
                _rawWorker.RecordOff();
                _isRawRecording = false;
            } catch (Exception ex) {
                MessageBox.Show($"Error occur while Raw record file is closing.:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RawPlay(object obj)
        {
            if (_isRawPalying == true)
                return;

            var dlg = new OpenFileDialog {
                Title = "Raw Record file save",
                Filter = "Raw Log (*.raw)|*.raw|모든 파일 (*.*)|*.*",
                FileName = $"RawLog_{DateTime.Now:yyyyMMdd_HHmmss}.raw", // 기본 파일명 예시
                DefaultExt = ".raw"
            };

            if (dlg.ShowDialog() == true) {
                string fileName = dlg.FileName;
                if (!File.Exists(fileName)) {
                    MessageBox.Show("File " + fileName + " does not exist");
                } else {
                    try {
                        _rawWorker.Start(fileName);
                        _isRawPalying = true;
                    } catch (Exception ex) {
                        MessageBox.Show("Cannot open file " + fileName + "\n" + ex.Message);
                    }
                }
            }
        }

        private void RawPlayStop(object obj)
        {
            if (_isRawPalying == false)
                return;

            _rawWorker.Stop();
            _isRawPalying = false;
        }

        private void SbsRecord(object obj)
        {   
            if (SbsConnectStatus != ConnectStatus.Connect) {
                MessageBox.Show("SBS is not connected.");
                return;
            }

            string path = "";

            if (ControlSettings.UseBigQuery)
            {
                // 2-1) BigQuery 이용하여 녹화
                try
                {
                    _db = new BigQuery("");
                    _sbsWorker.RecordOn(path, ControlSettings.UseBigQuery, _db);
                } catch (Exception ex)
                {
                    MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _sbsWorker.RecordOff();
                    return;
                }

                // 3) 녹화를 시작했다는 안내 메시지 (선택 사항)
                MessageBox.Show($"Start SBS record:\nQuery", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // 2-2) 기록 중이 아니면, SaveFileDialog 띄워서 파일 경로 선택
                var dlg = new SaveFileDialog {
                    Title = "SBS Record file save",
                    Filter = "SBS Log (*.sbs)|*.sbs|모든 파일 (*.*)|*.*",
                    FileName = $"SbsLog_{DateTime.Now:yyyyMMdd_HHmmss}.sbs", // 기본 파일명 예시
                    DefaultExt = ".sbs"
                };

                bool? result = dlg.ShowDialog();
                if (result != true) {
                    // 사용자가 취소했으면 아무 동작 없이 종료
                    return;
                }

                path = dlg.FileName;
                try {
                    _sbsWorker.RecordOn(path);
                } catch (Exception ex) {
                    MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _sbsWorker.RecordOff();
                    return;
                }

                // 4) 녹화를 시작했다는 안내 메시지 (선택 사항)
                MessageBox.Show($"Start SBS record:\n{path}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _isSbsRecording = true;
        }

        private void SbsRecordStop(object obj)
        {
            if (_isSbsRecording == false)
                return;

            try {
                _sbsWorker.RecordOff(ControlSettings.UseBigQuery);
                _db = null;
                _isSbsRecording = false;
            } catch (Exception ex) {
                MessageBox.Show($"SBS 기록 파일을 닫는 동안 오류가 발생했습니다:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SbsPlay(object obj)
        {
            if (_isSbsPalying == true)
                return;

            if (ControlSettings.UseBigQuery)
            {
                var items = BigQuery.GetTableLists();
                var win = new BigQueryListPopup(items);
                var res = win.ShowDialog();
                if (res == false)
                    return;

                var selItem = win.SelectedItem;

                _selectedBigQueryTable = selItem.Name;

                _db = new BigQuery(selItem.Name);
                _sbsWorker.Start(selItem.Name, true, _db);
            }
            else
            {
                var dlg = new OpenFileDialog {
                    Title = "SBS Record file save",
                    Filter = "SBS Log (*.sbs)|*.sbs|모든 파일 (*.*)|*.*",
                    FileName = $"SbsLog_{DateTime.Now:yyyyMMdd_HHmmss}.sbs", // 기본 파일명 예시
                    DefaultExt = ".sbs"
                };

                if (dlg.ShowDialog() == true) {
                    string fileName = dlg.FileName;
                    if (!File.Exists(fileName)) {
                        MessageBox.Show("File " + fileName + " does not exist");
                    } else {
                        try {
                            _sbsWorker.Start(fileName);
                        } catch (Exception ex) {
                            MessageBox.Show("Cannot open file " + fileName + "\n" + ex.Message);
                        }
                    }
                }
            }

            _isSbsPalying = true;
        }

        private void SbsPlayStop(object obj)
        {
            if (_isSbsPalying == false)
                return;

            _sbsWorker.Stop(ControlSettings.UseBigQuery);
            _db = null;
            _isSbsPalying = false;
        }

        private void RegisterEvents()
        {
            _mouseMoveSubscription = EventBus.Observe(EventIds.EvtMouseMoved).Subscribe(msg => UpdateMouseMove(msg));
        }

        private void UpdateMouseMove(object msg)
        {
            (double lat, double lon) mouse = ((double lat, double lon))msg;
            LatitudeOfMouse = DMS.DegreesMinutesSecondsLat(mouse.lat);
            LongitudeOfMouse = DMS.DegreesMinutesSecondsLon(mouse.lon);
        }

        private bool CanRawConnect(object obj) => RawConnectStatus == ConnectStatus.Disconnect;
        private bool CanRawDisconnect(object obj) => RawConnectStatus == ConnectStatus.Connect || SbsConnectStatus == ConnectStatus.Error;
        private bool CanSbsConnect(object obj) => SbsConnectStatus == ConnectStatus.Disconnect;
        private bool CanSbsDisconnect(object obj) => SbsConnectStatus == ConnectStatus.Connect || SbsConnectStatus == ConnectStatus.Error;

        public ICommand Cmd_RawConnect { get; }
        public ICommand Cmd_RawDisconnect { get; }
        public ICommand Cmd_SbsConnect { get; }
        public ICommand Cmd_SbsDisconnect { get; }

        public ICommand Cmd_RawRecord { get; }
        public ICommand Cmd_RawRecordStop { get; }
        public ICommand Cmd_RawPlay { get; }
        public ICommand Cmd_RawPlayStop { get; }
        public ICommand Cmd_SbsRecord { get; }
        public ICommand Cmd_SbsRecordStop { get; }
        public ICommand Cmd_SbsPlay { get; }
        public ICommand Cmd_SbsPlayStop { get; }
        public ICommand Cmd_Purge { get; }
        public ICommand Cmd_PolygonComplete { get; }
        public ICommand Cmd_Analytics { get; }
        //
        public ICommand InsertCommand { get; }
        public ICommand CompleteCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand MonitorCommand { get; }

        public ICommand Cmd_ShowCpaDialog { get; }
        private void RawDisconnect(object obj)
        {
            if (RawConnectStatus == ConnectStatus.Disconnect)
                return;

            try {
                _rawWorker.Stop();
                RawConnectStatus = ConnectStatus.Disconnect;
            } catch { }

            pingEcho.Stop();
        }

        private async void RawConnect(object obj)
        {
            if (RawConnectStatus == ConnectStatus.Connect)
                return;

            // 연결 중이 아니면 TextBox에 입력된 host:port로 연결 시도
            string input = ControlSettings.RawAddress.Trim();
            if (string.IsNullOrEmpty(input)) {
                MessageBox.Show("Raw Connect 주소를 입력하세요. (예: 127.0.0.1:30002)", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // host와 port 분리
            string host;
            int port;
            if (input.Contains(":")) {
                var parts = input.Split(new[] { ':' }, 2);
                host = parts[0];
                if (!int.TryParse(parts[1], out port)) {
                    MessageBox.Show("포트 번호가 올바르지 않습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            } else {
                // 포트 미입력 시 기본값 사용 (예: 30002)
                host = input;
                port = 30002;
            }

            // 비동기로 TCP 연결 시도
            try {
                CancellationTokenSource cts = new CancellationTokenSource();
                var popup = new LoadingPopup() { WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                                 Owner = Application.Current.MainWindow };
                popup.Closed += (s, e2) => { if (popup.IsCancelled) cts.Cancel(); };
                popup.Show();
                RawConnectStatus = ConnectStatus.Error;
                var res = await _rawWorker.Start(host, port, cts.Token);
                if (res) {
                    popup.Close();
                    RawConnectStatus = ConnectStatus.Connect;

                    pingEcho.Start(host, port, 2000, PingEchoHandler);
                } else {
                    MessageBox.Show("Connection Timeout.");
                }
            } catch (Exception ex) {
                logger.Error(ex);
                _rawWorker.Stop();
                RawConnectStatus = ConnectStatus.Disconnect;
            }
        }

        private void SbsDisconnect(object obj)
        {
            if (SbsConnectStatus == ConnectStatus.Disconnect)
                return;

            try
            {
                _sbsWorker.Stop();
                SbsConnectStatus = ConnectStatus.Disconnect;
            }
            catch { }

            pingEcho.Stop();
        }

        private CancellationTokenSource _sbsCts = new CancellationTokenSource();
        private async void SbsConnect(object obj)
        {
            if (SbsConnectStatus == ConnectStatus.Connect)
                return;

            string input = ControlSettings.SbsAddress.Trim();
            if (string.IsNullOrEmpty(input)) {
                MessageBox.Show("SBS Connect 주소를 입력하세요. (예: data.adsbhub.org:30003)", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // host와 port 분리
            string host;
            int port;
            if (input.Contains(":")) {
                var parts = input.Split(new[] { ':' }, 2);
                host = parts[0];
                if (!int.TryParse(parts[1], out port)) {
                    MessageBox.Show("포트 번호가 올바르지 않습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            } else {
                // 포트 미입력 시 기본값 사용 (예: 30003)
                host = input;
                port = 5002;
            }

            try {
                // 연결 후, 스트림에서 한 줄씩 읽어서 처리 (예시: OnSbsMessageReceived(rawLine))
               
                var popup = new LoadingPopup() {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow
                };
                popup.Closed += (s, e2) => { if (popup.IsCancelled) _sbsCts.Cancel(); };
                SbsConnectStatus = ConnectStatus.Error;
                popup.Show();
                var res = await _sbsWorker.Start(host, port, _sbsCts.Token);
                if (res) {
                    popup.Close();
                    SbsConnectStatus = ConnectStatus.Connect;

                    //pingEcho.Start(host, port, 10000, PingEchoHandler);

                } else {
                    MessageBox.Show("Connection Timeout.");
                }
            } catch (Exception ex) {
                logger.Error(ex);
                _sbsWorker.Stop();
                SbsConnectStatus = ConnectStatus.Disconnect;
            }
        }

        private async void PingEchoHandler(string host, int port, bool isConnected)
        {
            if (isConnected == false)
            {
                _sbsWorker.Stop();
                SbsConnectStatus = ConnectStatus.Error;
            }
            else
            {
                var res = await _sbsWorker.Start(host, port, _sbsCts.Token);
                if (res)
                {
                    SbsConnectStatus = ConnectStatus.Connect;
                }
            }
        }

        private void UpdateHookedAircraft()
        {
            if (AircraftManager.TrackHook.Valid_CC && AircraftManager.TryGet(AircraftManager.TrackHook.ICAO_CC, out Aircraft ac))
            {
                if (Aircraft == null) {
                    Aircraft = new AircraftForUI();
                }
                Aircraft.UpdateAircraftForUI(ac);
            }
            else
            {
                Aircraft = null;
            }
        }

        private void Purge(object obj)
        {
            AircraftManager.PurgeAll();
        }

        private AircraftForUI aircraft = null;
        public AircraftForUI Aircraft { get => aircraft; set => SetProperty(ref aircraft, value); }

        private ConnectStatus rawConnectStatus = ConnectStatus.Disconnect;
        public ConnectStatus RawConnectStatus { get => rawConnectStatus; set => SetProperty(ref rawConnectStatus, value); }

        private ConnectStatus sbsConnectStatus = ConnectStatus.Disconnect;
        public ConnectStatus SbsConnectStatus { get => sbsConnectStatus; set => SetProperty(ref sbsConnectStatus, value); }


        // Config
        public ControlSettings ControlSettings { get; set; } = new ControlSettings();

        public List<TileServerType> TileServerTypeList { get; set; } = Enum.GetValues(typeof(TileServerType)).Cast<TileServerType>().ToList();
        private TileServerType selectedTileServer = TileServerType.GoogleMaps;
        public TileServerType SelectedTileServer {
            get => selectedTileServer;
            set {
                SetProperty(ref selectedTileServer, value);
                MapManager.Instance.LoadMap(value);
            }
        }

        // Control
        private string latitudeOfMouse;
        public string LatitudeOfMouse { get => latitudeOfMouse; set => SetProperty(ref latitudeOfMouse, value); }

        private string longitudeOfMouse;
        public string LongitudeOfMouse { get => longitudeOfMouse; set => SetProperty(ref longitudeOfMouse, value); }

        public bool DisplayMapEnabled {
            get => ControlSettings.DisplayMapEnabled;
            set {
                ControlSettings.DisplayMapEnabled = value;
                OnPropertyChanged(nameof(DisplayMapEnabled));
                OnChangeSetting();
            }
        }

        private bool useTimeTogo;
        public bool UseTimeTogo {
            get => ControlSettings.UseTimeToGo;
            set {
                ControlSettings.UseTimeToGo = value;
                OnPropertyChanged(nameof(UseTimeTogo));
                OnChangeSetting();
            }
        }

        private double timeTogoValue;
        public double TimeTogoValue {
            get => ControlSettings.TimeToGoValue;
            set {
                ControlSettings.TimeToGoValue = value;
                OnPropertyChanged(nameof(TimeTogoValue));
                OnChangeSetting();
            }
        }

        private void OnChangeSetting()
        {
            EventBus.Publish(EventIds.EvtControlSettingChanged, ControlSettings);
        }

        private int numOfAircraft;
        public int NumOfAircraft { get => numOfAircraft; set => SetProperty(ref numOfAircraft, value); }

        private int viewableAircraft;
        public int ViewableAircraft { get => viewableAircraft; set => SetProperty(ref viewableAircraft, value); }

        private DateTime systemTime;
        public DateTime SystemTime { get => systemTime; set => SetProperty(ref systemTime, value); }

        private bool isLocal;
        public bool IsLocal
        {
            get => isLocal;
            set
            {
                SetProperty(ref isLocal, value);
                if (value)
                {
                    ControlSettings.SbsAddress = "128.237.96.41";
                    ControlSettings.UpdateUI();
                }
            }
        }

        private bool isHub;
        public bool IsHub
        {
            get => isHub;
            set
            {
                SetProperty(ref isHub, value);
                if (value)
                {
                    ControlSettings.SbsAddress = "data.adsbhub.org";
                    ControlSettings.UpdateUI();
                }
            }
        }

        private bool isEtc;
        public bool IsEtc
        {
            get => isEtc;
            set
            {
                SetProperty(ref isEtc, value);
                if (value)
                {
                    //ControlSettings.SbsAddress = "";
                    ControlSettings.UpdateUI();
                }
            }
        }

        private bool usePolygon;
        public bool UsePolygon
        {
            get => usePolygon;
            set
            {
                SetProperty(ref usePolygon, value);
                AreaManager.UsePolygon = value;
            }
        }
    }
}
