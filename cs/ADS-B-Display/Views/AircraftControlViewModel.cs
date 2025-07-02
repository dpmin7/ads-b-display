using ADS_B_Display;
using ADS_B_Display.Map;
using ADS_B_Display.Map.MapSrc;
using ADS_B_Display.Models;
using ADS_B_Display.Models.Settings;
using ADS_B_Display.Utils;
using ADS_B_Display.Views.Popup;
using ADS_B_Display.Views.UserControls;
using ADS_B_Display.Models.Parser;
using ADS_B_Display.Models.Connector;
using Microsoft.Win32;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ADS_B_Display.Views
{
    public class SpeedItem
    {
        public string Name { get; set; }
        public int Speed { get; set; }
        public SpeedItem(string name, int speed)
        {
            Name = name;
            Speed = speed;
        }
    }

    internal class AircraftControlViewModel : NotifyPropertyChangedBase, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private SbsWorker _sbsWorker = null;
        private SbsWorker _rawWorker = null;

        //Area Insert Module
        private bool _canCompleteOrCancel = false;
        public ObservableCollection<Area> AreaList { get; }
        public List<SpeedItem> Speeds { get; set; } = new List<SpeedItem>() { new SpeedItem("x1", 1), new SpeedItem("x2", 2), new SpeedItem("x3", 3), };
        private SpeedItem selSbsSpeed;
        private SpeedItem selRawSpeed;
        public SpeedItem SelRawSpeed
        {
            get => selRawSpeed;
            set
            {
                SetProperty(ref selRawSpeed, value);
                _rawWorker.setPlayBackSpeed(value.Speed);
            }
        }
        public SpeedItem SelSbsSpeed
        {
            get => selSbsSpeed;
            set
            {
                SetProperty(ref selSbsSpeed, value);
                _sbsWorker.setPlayBackSpeed(value.Speed);
            }
        }

        public bool CanAnalytics => _sbsWorker.ConnectorType == ConnectorType.DB && Aircraft != null;

        public List<Aircraft> ViewableAircraftList { get; private set; }
        private Area _selectedArea;
        private DispatcherTimer _timer = null;

        private PingEcho pingEcho = new PingEcho();
        IDBConnector _db;

        private string _tempAreaName { get; set; }

        private Color AreaColor { get; set; }

        private string _selectedBigQueryTable = null;

        public AircraftControlViewModel()
        {
            _sbsWorker = new SbsWorker(new SBSParser());
            _rawWorker = new SbsWorker(new RawParser());

            SelRawSpeed = Speeds[0];
            SelSbsSpeed = Speeds[0];

            RegisterEvents();

            ControlSettings = Setting.Instance.ControlSettings;
            if (ControlSettings == null)
            {
                ControlSettings = new ControlSettings();
                Setting.Instance.ControlSettings = ControlSettings;
            }

            OnChangeSetting();

            UsePurgeMode = ControlSettings.PurgeStale;
            PurgeDuration = ControlSettings.PurgeDuration;

            if (Enum.TryParse(ControlSettings.MapProvider, true, out TileServerType res))
                SelectedTileServer = res;
            else
                SelectedTileServer = TileServerType.GoogleMaps; // 기본값 설정

            AreaManager.LoadArea(ControlSettings.AreaList.Select(areaConfig => Area.AreaConfigToArea(areaConfig)).ToList());


            Cmd_RawConnect = new DelegateCommand(RawConnect, CanRawConnect);
            Cmd_RawDisconnect = new DelegateCommand(RawDisconnect, CanRawDisconnect);
            Cmd_SbsConnect = new DelegateCommand(SbsConnect, CanSbsConnect);
            Cmd_SbsDisconnect = new DelegateCommand(SbsDisconnect, CanSbsDisconnect);

            Cmd_RawRecord = new DelegateCommand(RawRecord);
            Cmd_RawRecordStop = new DelegateCommand(RawRecordStop);

            Cmd_RawPlay = new DelegateCommand(RawPlay);
            Cmd_RawPlayStop = new DelegateCommand(RawPlayStop);

            Cmd_SbsRecord = new DelegateCommand(SbsRecord);
            Cmd_SbsRecordStop = new DelegateCommand(SbsRecordStop);

            Cmd_SbsPlay = new DelegateCommand(SbsPlay);
            Cmd_SbsPlayStop = new DelegateCommand(SbsPlayStop);

            Cmd_Purge = new DelegateCommand(Purge);

            Cmd_Analytics = new DelegateCommand(Analytics);

            InsertCommand = new DelegateCommand(InsertArea, CanInsertArea);
            CompleteCommand = new DelegateCommand(CompleteArea, CanCompleteArea);
            CancelCommand = new DelegateCommand(CancelArea, CanCancelArea);
            DeleteCommand = new DelegateCommand(DeleteArea, CanDeleteArea);
            EditCommand = new DelegateCommand(EditArea, CanEditArea);
            MonitorCommand = new DelegateCommand(MonitorArea, CanMonitorArea);

            AreaList = new ObservableCollection<Area>(AreaManager.Areas);
            ViewableAircraftList = new List<Aircraft>();
            Cmd_ShowCpaDialog = new DelegateCommand(ShowCpaDialog);
            Cmd_SetAircraftType = new DelegateCommand(SetAircraftType);
            Cmd_ZoomIn = new DelegateCommand(ZoomIn);
            Cmd_ZoomOut = new DelegateCommand(ZoomOut);
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += (s, e) =>
            {
                ViewableAircraftList = AircraftManager.GetAllOnScreen();
                NumOfFilteredAircraft = AircraftManager.NumOfFilteredAircraft;
                NumOfAircraft = AircraftManager.Count();
                SystemTime = DateTime.Now;
                UpdateHookedAircraft();

                OnPropertyChanged("ViewableAircraftList");
                OnPropertyChanged("CanAnalytics");
            };

            _timer.Start();
        }
        private void ZoomIn(object obj)
        {
            EventBus.Publish(EventIds.EvtZoom, true);
        }

        private void ZoomOut(object obj)
        {
            EventBus.Publish(EventIds.EvtZoom, false);
        }

        private Window TypeSettingWindow = null;
        private IList<string> _selectedAircraftTypeList = new List<string>();
        private IList<string> AircraftTypeList { get; set; } = new List<string>();
        private bool _useAircraftTypeFilter = false;
        public bool UseAircraftTypeFilter
        {
            get => _useAircraftTypeFilter;
            set
            {
                if (_useAircraftTypeFilter != value)
                {
                    _useAircraftTypeFilter = value;
                    OnPropertyChanged(nameof(UseAircraftTypeFilter));
                    AircraftManager.UpdateUseAircraftTypeFilter(value);
                }
            }
        }

        private void SetAircraftType(object obj)
        {
            if (AircraftTypeList.Count == 0)
                return;

            Window TypeSettingWindow = new TypeFilterSettingPopup()
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                DataContext = new TypeFilterSettingPopupVM(AircraftTypeList, _selectedAircraftTypeList, OnAircraftTypeFilterChanged)
            };
            TypeSettingWindow.Show();
        }

        private void OnAircraftTypeFilterChanged(List<string> types)
        {
            _selectedAircraftTypeList = types.ToList();
            //_useAircraftTypeFilter = true;
            AircraftManager.UpdateAircraftTypeFilter(_selectedAircraftTypeList);
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
            try
            {
                var dialog = new CPAConflictDialog
                {
                    Owner = Application.Current.MainWindow
                };

                // 현재는 SelectedConflict를 사용하지 않으므로, 이벤트 등록 없이 Show만 실행
                // 필요시 안전하게 처리하려면 다음처럼 작성 가능
                /*
                dialog.Closed += (sender, e) =>
                {
                    try
                    {
                        if (dialog.SelectedConflict != null)
                        {
                            var selected = dialog.SelectedConflict;
                            double lat = (selected.Lat1 + selected.Lat2) / 2;
                            double lon = (selected.Lon1 + selected.Lon2) / 2;

                            // 예외 방지용: 지도 컴포넌트가 null이 아닌 경우에만 호출
                            airScreenViewControl?.CenterMapTo(lat, lon);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 예외 로그 기록 등
                        Debug.WriteLine($"[CPA Dialog Closed] Exception: {ex.Message}");
                    }
                };
                */

                dialog.Show(); // 비동기 방식으로 열기
            }
            catch (Exception ex)
            {
                // 예외 발생 시에도 앱이 죽지 않도록 처리
                //Debug.WriteLine($"[ShowCpaDialog] Failed to show dialog: {ex.Message}");
            }
        }

        private bool _isRawRecording = false;
        public bool IsRawRecording
        {
            get => _isRawRecording;
            set
            {
                if (_isRawRecording != value)
                {
                    _isRawRecording = value;
                    OnPropertyChanged(nameof(IsRawRecording));
                    ((DelegateCommand)Cmd_RawRecordStop).RaiseCanExecuteChanged();
                    ((DelegateCommand)Cmd_RawPlay).RaiseCanExecuteChanged();
                }
            }
        }
        private bool _isRawPalying = false;
        public bool IsRawPalying
        {
            get => _isRawPalying;
            set
            {
                if (_isRawPalying != value)
                {
                    _isRawPalying = value;
                    OnPropertyChanged(nameof(IsRawPalying));
                    ((DelegateCommand)Cmd_RawPlayStop).RaiseCanExecuteChanged();
                    ((DelegateCommand)Cmd_RawRecord).RaiseCanExecuteChanged();
                }
            }
        }
        private bool _isSbsRecording = false;
        public bool IsSbsRecording
        {
            get => _isSbsRecording;
            set
            {
                if (_isSbsRecording != value)
                {
                    _isSbsRecording = value;
                    OnPropertyChanged(nameof(IsSbsRecording));
                    ((DelegateCommand)Cmd_SbsRecordStop).RaiseCanExecuteChanged();
                    ((DelegateCommand)Cmd_SbsPlay).RaiseCanExecuteChanged();
                }
            }
        }
        private bool _isSbsPalying = false;
        public bool IsSbsPalying
        {
            get => _isSbsPalying;
            set
            {
                if (_isSbsPalying != value)
                {
                    _isSbsPalying = value;
                    OnPropertyChanged(nameof(IsSbsPalying));
                    ((DelegateCommand)Cmd_SbsPlayStop).RaiseCanExecuteChanged();
                    ((DelegateCommand)Cmd_SbsRecord).RaiseCanExecuteChanged();
                }
            }
        }

        private IDisposable _mouseMoveSubscription;

        private bool CanRawRecord(object obj) => !IsRawRecording && RawConnectStatus == ConnectStatus.Connect;//RawConnectStatus == ConnectStatus.Connect; // connect 시에 _isRecord, isPlaying, isPlayStopped 초기화
        private bool CanRawRecordStop(object obj) => IsRawRecording && RawConnectStatus == ConnectStatus.Connect;//RawConnectStatus == ConnectStatus.Connect; // connect 시에 _isRecord, isPlaying, isPlayStopped 초기화
        private bool CanRawPlay(object obj) => !IsRawPalying && RawConnectStatus == ConnectStatus.Disconnect;//RawConnectStatus == ConnectStatus.Disconnect && ;
        private bool CanRawPlayStop(object obj) => IsRawPalying && RawConnectStatus == ConnectStatus.Disconnect;// !_isRawRecording;

        private bool CanSbsRecord(object obj) => !IsSbsRecording && SbsConnectStatus == ConnectStatus.Connect;//!_isRawRecording;
        private bool CanSbsRecordStop(object obj) => IsSbsRecording && SbsConnectStatus == ConnectStatus.Connect;//!_isRawRecording;
        private bool CanSbsPlay(object obj) => !IsSbsPalying && SbsConnectStatus == ConnectStatus.Disconnect;//!_isRawRecording;
        private bool CanSbsPlayStop(object obj) => IsSbsPalying && SbsConnectStatus == ConnectStatus.Disconnect;//!_isRawRecording;

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

        private void EditArea(object obj)
        {
            if (SelectedArea != null && obj is int idx && idx >= 0)
            {
                AreaRegisterPopup popup = new AreaRegisterPopup
                {
                    AreaName = SelectedArea.Name,
                    AreaColor = SelectedArea.Color
                };
                popup.Owner = Application.Current.MainWindow;
                popup.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var res = popup.ShowDialog();
                if (res == true)
                {
                    SelectedArea.Name = popup.AreaName;
                    SelectedArea.Color = popup.AreaColor;
                    //if (AreaManager.EditArea(SelectedArea, name, color, idx))
                    SelectedArea.UpdateUI();
                }
            }
        }

        private bool CanMonitorArea(object obj) => true;


        private void MonitorArea(object obj)
        {
            AreaMonitorPopup monitor = new AreaMonitorPopup
            {
                Topmost = true, // 항상 위에 표시
                Owner = Application.Current.MainWindow // 필요 시 소유자 지정 (선택)
            };
            monitor.Show(); // 모달리스로 띄우기
        }

        private bool CanDeleteArea(object obj) => SelectedArea != null;
        private bool CanEditArea(object obj) => SelectedArea != null;


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
            var dlg = new SaveFileDialog
            {
                Title = "Raw Record file save",
                Filter = "Raw Log (*.raw)|*.raw|모든 파일 (*.*)|*.*",
                FileName = $"RawLog_{DateTime.Now:yyyyMMdd_HHmmss}.raw", // 기본 파일명 예시
                DefaultExt = ".raw"
            };

            bool? result = dlg.ShowDialog();
            if (result != true)
            {
                // 사용자가 취소했으면 아무 동작 없이 종료
                return;
            }

            string path = dlg.FileName;
            try
            {
                _rawWorker.RecordOn(path, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _rawWorker.RecordOff();
                return;
            }

            IsRawRecording = true;
        }

        private void RawRecordStop(object obj)
        {
            if (_isRawRecording == false)
                return;

            try
            {
                _rawWorker.RecordOff();
                IsRawRecording = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error occur while Raw record file is closing.:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RawPlay(object obj)
        {
            if (_isRawPalying == true)
                return;

            var dlg = new OpenFileDialog
            {
                Title = "Raw Record file save",
                Filter = "Raw Log (*.raw)|*.raw|모든 파일 (*.*)|*.*",
                FileName = $"RawLog_{DateTime.Now:yyyyMMdd_HHmmss}.raw", // 기본 파일명 예시
                DefaultExt = ".raw"
            };

            if (dlg.ShowDialog() == true)
            {
                string fileName = dlg.FileName;
                if (!File.Exists(fileName))
                {
                    MessageBox.Show("File " + fileName + " does not exist");
                }
                else
                {
                    try
                    {
                        _rawWorker.Start(fileName);
                        IsRawPalying = true;
                    }
                    catch (Exception ex)
                    {
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
            IsRawPalying = false;
        }

        private void SbsRecord(object obj)
        {
            if (SbsConnectStatus != ConnectStatus.Connect)
            {
                MessageBox.Show("SBS is not connected.");
                return;
            }

            string path = "";

            if (ControlSettings.UseBigQuery)
            {
                // 2-1) BigQuery 이용하여 녹화
                try
                {
                    _db = new BigQueryConnector("");
                    _sbsWorker.RecordOn(path, ControlSettings.UseBigQuery, _db);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _sbsWorker.RecordOff();
                    return;
                }
            }
            else
            {
                // 2-2) 기록 중이 아니면, SaveFileDialog 띄워서 파일 경로 선택
                var dlg = new SaveFileDialog
                {
                    Title = "SBS Record file save",
                    Filter = "SBS Log (*.sbs)|*.sbs|모든 파일 (*.*)|*.*",
                    FileName = $"SbsLog_{DateTime.Now:yyyyMMdd_HHmmss}.sbs", // 기본 파일명 예시
                    DefaultExt = ".sbs"
                };

                bool? result = dlg.ShowDialog();
                if (result != true)
                {
                    // 사용자가 취소했으면 아무 동작 없이 종료
                    return;
                }

                path = dlg.FileName;
                try
                {
                    _sbsWorker.RecordOn(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Can not open the recorded file.:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _sbsWorker.RecordOff();
                    return;
                }
            }

            IsSbsRecording = true;
        }

        private void SbsRecordStop(object obj)
        {
            if (_isSbsRecording == false)
                return;

            try
            {
                _sbsWorker.RecordOff();
                _db = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while closing the SBS log file.:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSbsRecording = false;
            }
        }

        private IDisposable _currentTimeSubscription;
        private void SbsPlay(object obj)
        {
            if (_isSbsPalying == true)
                return;

            if (ControlSettings.UseBigQuery)
            {
                var items = BigQueryConnector.GetTableLists();
                if (items == null)
                {
                    IsSbsPalying = false;
                    return;
                }
                var win = new BigQueryListPopup(items);
                win.Owner = Application.Current.MainWindow;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var res = win.ShowDialog();
                if (res == false)
                {
                    IsSbsPalying = false;
                    return;
                }

                var selItem = win.SelectedItem;

                _selectedBigQueryTable = selItem.Name;

                _db = new BigQueryConnector(selItem.Name);
                (long st, long ed) times = _db.GetPlaybackTime(selItem.Name);
                EdTime = times.ed;
                StTime = RemainTime = times.st;
                _currentTimeSubscription = EventBus.Observe(EventIds.EvtBigQueryRemainTimeUpdate).Subscribe(time =>
                {
                    RemainTime = (long)time;
                    OnPropertyChanged(nameof(RemainTime));
                });
                _db.StartPlayTiming();
                _sbsWorker.Start(selItem.Name, true, _db);
            }
            else
            {
                var dlg = new OpenFileDialog
                {
                    Title = "SBS Record file save",
                    Filter = "SBS Log (*.sbs)|*.sbs|모든 파일 (*.*)|*.*",
                    FileName = $"SbsLog_{DateTime.Now:yyyyMMdd_HHmmss}.sbs", // 기본 파일명 예시
                    DefaultExt = ".sbs"
                };

                if (dlg.ShowDialog() == true)
                {
                    string fileName = dlg.FileName;
                    if (!File.Exists(fileName))
                    {
                        MessageBox.Show("File " + fileName + " does not exist");
                    }
                    else
                    {
                        try
                        {
                            _sbsWorker.Start(fileName);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Cannot open file " + fileName + "\n" + ex.Message);
                        }
                    }
                }
            }

            IsSbsPalying = true;
        }

        private void SbsPlayStop(object obj)
        {
            if (_isSbsPalying == false)
                return;

            _sbsWorker.Stop(ControlSettings.UseBigQuery);
            _db = null;
            IsSbsPalying = false;
        }

        private void RegisterEvents()
        {
            _mouseMoveSubscription = EventBus.Observe(EventIds.EvtMouseMoved).Subscribe(msg => UpdateMouseMove(msg));
            EventBus.Observe(EventIds.EvtAircraftDBInitialized).
                Subscribe(msg => AircraftTypeList = AircraftDB.AircraftTypes.ToList());
        }

        private void UpdateMouseMove(object msg)
        {
            (double lat, double lon) mouse = ((double lat, double lon))msg;
            LatitudeOfMouse = DMS.DegreesMinutesSecondsLat(mouse.lat);
            LongitudeOfMouse = DMS.DegreesMinutesSecondsLon(mouse.lon);
        }

        private bool CanRawConnect(object obj) => RawConnectStatus == ConnectStatus.Disconnect;
        private bool CanRawDisconnect(object obj) => RawConnectStatus == ConnectStatus.Connect || RawConnectStatus == ConnectStatus.Error;
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
        public ICommand EditCommand { get; }
        public ICommand MonitorCommand { get; }

        public ICommand Cmd_ShowCpaDialog { get; }
        public ICommand Cmd_SetAircraftType { get; }
        public ICommand Cmd_ZoomIn { get; }
        public ICommand Cmd_ZoomOut { get; }
        private void RawDisconnect(object obj)
        {
            if (RawConnectStatus == ConnectStatus.Disconnect)
                return;

            try
            {
                _rawWorker.Stop();
                RawConnectStatus = ConnectStatus.Disconnect;
            }
            catch { }

            pingEcho.Stop();
        }

        private async void RawConnect(object obj)
        {
            if (RawConnectStatus == ConnectStatus.Connect)
                return;

            logger.Info($"[RAW] Connect Button Click: {DateTime.Now:HH:mm:ss.fff}");
            AirScreenPanelView.connectStartTime = DateTime.Now;

            // 연결 중이 아니면 TextBox에 입력된 host:port로 연결 시도
            string input = ControlSettings.RawAddress.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Enter the Raw Connect address. (e.g., 127.0.0.1:30002)", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Purge(null); // 일단 화면 데이터 날리자.

            // host와 port 분리
            string host;
            int port;
            if (input.Contains(":"))
            {
                var parts = input.Split(new[] { ':' }, 2);
                host = parts[0];
                if (!int.TryParse(parts[1], out port))
                {
                    MessageBox.Show("The port number is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                // 포트 미입력 시 기본값 사용 (예: 30002)
                host = input;
                port = 30002;
            }

            // 비동기로 TCP 연결 시도
            try
            {
                var popup = new LoadingPopup()
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow
                };
                popup.Closed += (s, e2) => {
                    if (popup.IsCancelled) { }
                        //_rawCts.Cancel(); 
                };
                popup.Show();
                RawConnectStatus = ConnectStatus.Error;
                var res = await _rawWorker.Start(host, port, _rawCts.Token);
                if (res)
                {
                    popup.Close();
                    RawConnectStatus = ConnectStatus.Connect;

                    pingEcho.Start(host, port, 2000, PingEchoRawHandler);
                }
                else
                {
                    MessageBox.Show("Connection Timeout.");
                    RawConnectStatus = ConnectStatus.Error;
                }
            }
            catch (Exception ex)
            {
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
        private CancellationTokenSource _rawCts = new CancellationTokenSource();
        private async void SbsConnect(object obj)
        {
            logger.Info($"Connect SBS {SbsConnectStatus}");

            if (SbsConnectStatus == ConnectStatus.Connect)
                return;

            logger.Info($"[SBS] Connect Button Click: {DateTime.Now:HH:mm:ss.fff}");
            AirScreenPanelView.connectStartTime = DateTime.Now;

            string input = ControlSettings.SbsAddress.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Enter the SBS Connect address. (e.g., data.adsbhub.org:30003)", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Purge(null); // 일단 화면 데이터 날리자.

            // host와 port 분리
            string host;
            int port;
            if (input.Contains(":"))
            {
                var parts = input.Split(new[] { ':' }, 2);
                host = parts[0];
                if (!int.TryParse(parts[1], out port))
                {
                    MessageBox.Show("The port number is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                // 포트 미입력 시 기본값 사용 (예: 30003)
                host = input;
                port = 5002;
            }

            try
            {
                // 연결 후, 스트림에서 한 줄씩 읽어서 처리 (예시: OnSbsMessageReceived(rawLine))

                var popup = new LoadingPopup()
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow
                };
                popup.Closed += (s, e2) => {
                    if (popup.IsCancelled) { }
                        //_sbsCts.Cancel();
                };
                SbsConnectStatus = ConnectStatus.Error;
                popup.Show();
                var res = await _sbsWorker.Start(host, port, _sbsCts.Token);
                if (res)
                {
                    popup.Close();
                    SbsConnectStatus = ConnectStatus.Connect;

                    pingEcho.Start(host, port, 10000, PingEchoHandler);

                }
                else
                {
                    MessageBox.Show("Connection Timeout.");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                _sbsWorker.Stop();
                SbsConnectStatus = ConnectStatus.Disconnect;
            }
        }

        private async void PingEchoRawHandler(string host, int port, bool isConnected)
        {
            if (isConnected == false)
            {
                _rawWorker.Stop();
                RawConnectStatus = ConnectStatus.Error;
            }
            else
            {
                var res = await _rawWorker.Start(host, port, _rawCts.Token);
                if (res)
                {
                    RawConnectStatus = ConnectStatus.Connect;
                }
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
                if (Aircraft == null)
                {
                    Aircraft = new AircraftForUI();
                }

                ac.AircraftData.Dep = AircraftManager.TrackHook.DepartureAirport != null ? AircraftManager.TrackHook.DepartureAirport["ICAO"] : "NA";
                ac.AircraftData.Arr = AircraftManager.TrackHook.ArrivalAirport != null ? AircraftManager.TrackHook.ArrivalAirport["ICAO"] : "NA";

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
        public TileServerType SelectedTileServer
        {
            get => selectedTileServer;
            set
            {
                SetProperty(ref selectedTileServer, value);
                MapManager.Instance.LoadMap(value);
            }
        }

        // Control
        private string latitudeOfMouse;
        public string LatitudeOfMouse { get => latitudeOfMouse; set => SetProperty(ref latitudeOfMouse, value); }

        private string longitudeOfMouse;
        public string LongitudeOfMouse { get => longitudeOfMouse; set => SetProperty(ref longitudeOfMouse, value); }

        public bool DisplayMapEnabled
        {
            get => ControlSettings.DisplayMapEnabled;
            set
            {
                ControlSettings.DisplayMapEnabled = value;
                OnPropertyChanged(nameof(DisplayMapEnabled));
                OnChangeSetting();
            }
        }

        private bool useTimeTogo;
        public bool UseTimeTogo
        {
            get => ControlSettings.UseTimeToGo;
            set
            {
                ControlSettings.UseTimeToGo = value;
                OnPropertyChanged(nameof(UseTimeTogo));
                OnChangeSetting();
            }
        }

        private double timeTogoValue;
        public double TimeTogoValue
        {
            get => ControlSettings.TimeToGoValue;
            set
            {
                ControlSettings.TimeToGoValue = value;
                OnPropertyChanged(nameof(TimeTogoValue));
                OnChangeSetting();
            }
        }

        private void OnChangeSetting()
        {
            EventBus.Publish(EventIds.EvtControlSettingChanged, ControlSettings);
        }

        internal void MouseDoubleClick()
        {
            if (SelectedArea != null)
            {
                EventBus.Publish(EventIds.EvtCenterMapTo, (SelectedArea.Points[0].Y, SelectedArea.Points[0].X));
            }
        }

        private int numOfAircraft;
        public int NumOfAircraft { get => numOfAircraft; set => SetProperty(ref numOfAircraft, value); }

        private int numOfFilteredAircraft;
        public int NumOfFilteredAircraft { get => numOfFilteredAircraft; set => SetProperty(ref numOfFilteredAircraft, value); }

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

        private bool isEtc = true;
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


        private bool useSpeedFilter = false;

        public bool UseSpeedFilter
        {
            get => useSpeedFilter;
            set
            {
                SetProperty(ref useSpeedFilter, value);
                AircraftManager.UpdateSpeedFilter(useSpeedFilter, minSpeed, maxSpeed);
            }
        }

        private double minSpeed = 0;

        public double MinSpeed
        {
            get => minSpeed;
            set
            {
                SetProperty(ref minSpeed, value);
                AircraftManager.UpdateSpeedFilter(useSpeedFilter, minSpeed, maxSpeed);
            }
        }

        private double maxSpeed = 1500;

        public double MaxSpeed
        {
            get => maxSpeed;
            set
            {
                SetProperty(ref maxSpeed, value);
                AircraftManager.UpdateSpeedFilter(useSpeedFilter, minSpeed, maxSpeed);
            }
        }

        private double minAltitude = 0;

        public double MinAltitude
        {
            get => minAltitude;
            set
            {
                SetProperty(ref minAltitude, value);
                AircraftManager.UpdateAltitudeFilter(useAltitudeFilter, minAltitude, maxAltitude);
            }
        }

        private double maxAltitude = 50000;

        public double MaxAltitude
        {
            get => maxAltitude;
            set
            {
                SetProperty(ref maxAltitude, value);
                AircraftManager.UpdateAltitudeFilter(useAltitudeFilter, minAltitude, maxAltitude);
            }
        }

        private bool useAltitudeFilter = false;

        public bool UseAltitudeFilter
        {
            get => useAltitudeFilter;
            set
            {
                SetProperty(ref useAltitudeFilter, value);
                AircraftManager.UpdateAltitudeFilter(useAltitudeFilter, minAltitude, maxAltitude);
            }
        }

        private long remainTime;
        public long RemainTime { get => remainTime; set => SetProperty(ref remainTime, value); }

        private long edTime;
        public long EdTime { get => edTime; set => SetProperty(ref edTime, value); }

        private long stTime;
        public long StTime { get => stTime; set => SetProperty(ref stTime, value); }

        private bool useGhostMode = true;
        public bool UseGhostMode {
            get => useGhostMode;
            set
            {
                SetProperty(ref useGhostMode, value);
                AircraftManager.SetUseGhost(value);
            }
        }

        private long ghostDuration = 20;
        public long GhostDuration {
            get => ghostDuration;
            set
            {
                SetProperty(ref ghostDuration, value);
                AircraftManager.SetGhostLimitMS(value * 1000);
            }
        }

        private bool usePurgeMode = true;
        public bool UsePurgeMode
        {
            get => usePurgeMode;
            set
            {
                SetProperty(ref usePurgeMode, value); 
                ControlSettings.PurgeStale = value;
                AircraftManager.SetUsePurge(value);
            }
        }

        private long purgeDuration = 30;
        public long PurgeDuration
        {
            get => purgeDuration;
            set
            {
                SetProperty(ref purgeDuration, value);
                ControlSettings.PurgeDuration = value;
                AircraftManager.SetPurgeLimitMS(value * 1000);
            }
        }

        private bool filterMilitary;
        public bool FilterMilitary {
            get => filterMilitary;
            set
            {
                SetProperty(ref filterMilitary, value);
                AircraftManager.SetMilitaryFilter(filterMilitary);
            }
        }
    }
}
