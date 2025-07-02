using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace ADS_B_Display.Models
{
    internal static class AircraftManager
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static readonly Dictionary<uint, Aircraft> _aircraftTable
            = new Dictionary<uint, Aircraft>();

        private static TrackHookStruct _trackHook = new TrackHookStruct();
        public static TrackHookStruct TrackHook => _trackHook;

        private static object lockObj = new object();

        private static Timer _dataTimer;
        private static bool _usePurge = true; // Purge 기능 활성화 여부
        private static long _purgeLimitMS = 30000; // 30초 (1분) 후에 Purge
        private static bool _useghost = true; // Ghost 기능 활성화 여부
        private static long _ghostLimitMS = 20000; // 10초 (1분) 후에 Purge

        private static ObservableCollection<CPAConflictInfo> _cpaConflicts = new ObservableCollection<CPAConflictInfo>();
        public static ObservableCollection<CPAConflictInfo> CPAConflicts => _cpaConflicts;

        private static string _focusedAircraftHex1 = null;
        private static string _focusedAircraftHex2 = null;

        /// <summary>
        /// 포커스한 항공기 식별자 저장
        /// </summary>
        public static void SetFocusedAircraft(string hex1, string hex2)
        {
            lock (lockObj)
            {
                _focusedAircraftHex1 = hex1;
                _focusedAircraftHex2 = hex2;
            }
        }

        /// <summary>
        /// 포커스한 항공기 식별자 가져오기
        /// </summary>
        public static void GetFocusedAircraft(out string hex1, out string hex2)
        {
            lock (lockObj)
            {
                hex1 = _focusedAircraftHex1;
                hex2 = _focusedAircraftHex2;
            }
        }

        public static bool IsFocusedAircraft(string hexAddr)
        {
            lock (lockObj)
            {
                if (_focusedAircraftHex1 == hexAddr || _focusedAircraftHex2 == hexAddr)
                    return true;
            }
            return false;
        }

        public static List<CPAConflictInfo> GetCPAConflicts()
        {
            lock (lockObj)
            {
                return _cpaConflicts.ToList();
            }
        }

        public static bool HasCPAConflict(Aircraft aircraft)
        {
            if (aircraft == null)
                return false;

            uint icao = aircraft.ICAO;

            lock (lockObj)
            {
                return _cpaConflicts.Any(conflict => conflict.ICAO1 == icao || conflict.ICAO2 == icao);
            }
        }

        static AircraftManager()
        {
            // Timer 설정: 1초마다 데이터 업데이트
            _dataTimer = new Timer(300);
            _dataTimer.Elapsed += OnDataTimerElapsed;
            _dataTimer.AutoReset = true;
            _dataTimer.Enabled = true;


        }
        public static void AddCPAConflict(CPAConflictInfo conflict)
        {
            lock (lockObj)
            {
                _cpaConflicts.Add(conflict);
            }
        }

        public static void ClearCPAConflicts()
        {
            lock (lockObj)
            {
                _cpaConflicts.Clear();
            }
        }

        public static void UpdateCPAConflicts(List<CPAConflictInfo> newConflicts)
        {
            lock (lockObj)
            {
                foreach (var conflict in _cpaConflicts)
                {
                    if (TryGet(conflict.ICAO1, out Aircraft aircraft1))
                    {
                        aircraft1.IsConflictRisk = false;
                    }
                    if (TryGet(conflict.ICAO2, out Aircraft aircraft2))
                    {
                        aircraft2.IsConflictRisk = false;
                    }
                }
                _cpaConflicts.Clear();

                foreach (var conflict in newConflicts)
                {
                    _cpaConflicts.Add(conflict);
                    if (TryGet(conflict.ICAO1, out Aircraft aircraft1))
                    {
                        aircraft1.IsConflictRisk = true;
                    }
                    if (TryGet(conflict.ICAO2, out Aircraft aircraft2))
                    {
                        aircraft2.IsConflictRisk = true;
                    }
                }
            }
        }

        internal static void SetUsePurge(bool usePurge)
        {
            lock (lockObj)
            {
                _usePurge = usePurge;
            }
        }

        internal static void SetPurgeLimitMS(long purgelimitMS)
        {
            lock (lockObj)
            {
                _purgeLimitMS = purgelimitMS;
            }
        }

        internal static void SetUseGhost(bool useGhost)
        {
            lock (lockObj)
            {
                _useghost = useGhost;
            }
        }

        internal static void SetGhostLimitMS(long ghostLimitMS)
        {
            lock (lockObj)
            {
                _ghostLimitMS = ghostLimitMS;
            }
        }

        // Purge, virtual 업데이트
        private static void OnDataTimerElapsed(object sender, ElapsedEventArgs e)
        {
            long now = TimeFunctions.GetCurrentTimeInMsec();
            lock (lockObj)
            {
                var keysToRemove = new List<uint>();

                // 첫 번째 루프: 
                // 1. TimeCheck함수에서 현재 시간과 비교하여 Purge 대상 키를 수집, Ghost상태 체크, 가상 위치 계산
                // 2. 다각형 등의 필터도 여기서 하기...
                var num = 0;
                foreach (var kvp in _aircraftTable)
                {
                    if (kvp.Value.TimeCheck(now, _ghostLimitMS, _purgeLimitMS, _useghost, _usePurge))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                    if (_speedFilter.IsValid(kvp.Value.Speed) &&
                        _altitudeFilter.IsValid(kvp.Value.Altitude) &&
                        _aircraftTypeFilter.IsValid(kvp.Value.AircraftData.IcaoAircraftType))
                    {
                        kvp.Value.Filtered = false;
                        num++;
                    }
                    else
                    {
                        kvp.Value.Filtered = true;
                    }
                }

                NumOfFilteredAircraft = num;

                // 두 번째 루프: Purge키로 aircraft 삭제
                foreach (var key in keysToRemove)
                {
                    _aircraftTable.Remove(key);
                }
            }

            UpdateViewableAircraftInPolygon();
        }

        internal static void FilterTest(Aircraft aircraft)
        {
            if (_speedFilter.IsValid(aircraft.Speed) &&
                _altitudeFilter.IsValid(aircraft.Altitude) &&
                _aircraftTypeFilter.IsValid(aircraft.AircraftData.IcaoAircraftType))
            {
                aircraft.Filtered = true;
            }
        }

        internal static Aircraft GetOrAdd(uint icao)
        {
            if (!_aircraftTable.TryGetValue(icao, out var aircraft))
            {
                aircraft = new Aircraft
                {
                    ICAO = icao,
                    HexAddr = icao.ToString("X6"),
                    NumMessagesSBS = 0,
                    NumMessagesRaw = 0,
                    VerticalRate = 0,
                    HaveAltitude = false,
                    HaveLatLon = false,
                    HaveSpeedAndHeading = false,
                    HaveFlightNum = false,
                    // SpriteImage 및 CycleImages 로직은 UI 쪽에서 설정
                };
                lock (lockObj)
                {
                    _aircraftTable.Add(icao, aircraft);
                }
            }
            aircraft.AircraftData = AircraftDB.GetAircraftInfo(aircraft.ICAO);
            aircraft.AircraftData.OperatorIcao = Airlines.GetAirlineFromCallsign(aircraft.FlightNum);

            return aircraft;
        }

        internal static bool TryGet(uint icao, out Aircraft aircraft)
        {
            return _aircraftTable.TryGetValue(icao, out aircraft);
        }

        internal static IEnumerable<Aircraft> GetAll()
        {
            lock (lockObj)
            {
                return _aircraftTable.Values.ToList();
            }
        }

        internal static List<Aircraft> GetAllOnScreen()
        {
            lock (lockObj)
            {
                return _aircraftTable.Values.Where(a => a.OnScreen).ToList();
            }
        }

        internal static int Count()
        {
            lock (lockObj)
            {
                return _aircraftTable.Count;
            }
        }

        public static void UpdateSpeedFilter(bool use, double min, double max)
        {
            lock (lockObj)
            {
                _speedFilter.UseFilter = use;
                _speedFilter.Min = min;
                _speedFilter.Max = max;
            }
        }

        public static void UpdateAltitudeFilter(bool use, double min, double max)
        {
            lock (lockObj)
            {
                _altitudeFilter.UseFilter = use;
                _altitudeFilter.Min = min;
                _altitudeFilter.Max = max;
            }
        }

        public static void UpdateAircraftTypeFilter(IList<string> types)
        {
            lock (lockObj)
            {
                _aircraftTypeFilter.Types = types.ToList();
            }
        }

        public static void UpdateUseAircraftTypeFilter(bool use)
        {
            lock (lockObj)
            {
                _aircraftTypeFilter.UseFilter = use;
            }
        }

        public static int NumOfFilteredAircraft { get; private set; }
        private static Filter _speedFilter = new Filter();
        private static Filter _altitudeFilter = new Filter();
        private static TypeFilter _aircraftTypeFilter = new TypeFilter();

        private static double _onLeftTopLat = 0.0;
        private static double _onRightBottomLat = 0.0;
        private static double _onLeftTopLon = 0.0;
        private static double _onRightBottomLon = 0.0;
        internal static void UpdateOnScreen(double leftTopLat, double leftTopLon, double rightBottomLat, double rightBottomLon)
        {
            _onLeftTopLat = leftTopLat;
            _onLeftTopLon = leftTopLon;
            _onRightBottomLat = rightBottomLat;
            _onRightBottomLon = rightBottomLon;
        }

        internal static void PurgeAll()
        {
            lock (lockObj) {
                _aircraftTable.Clear();
            }
        }

        internal static void UpdateViewableAircraftInPolygon()
        {
            lock (lockObj)
            {
                foreach (var aircraft in _aircraftTable.Values)
                {
                    bool outCheck = false;
                    bool isInAnyArea = false;
                    
                    foreach (var area in AreaManager.Areas)
                    {
                        if (area.Use == false)
                        {
                            area.SetViewable(false);
                            continue;
                        }
                        if (PointPolygonFilter.IsPointInArea(aircraft.Latitude,
                            aircraft.Longitude, area.Points.ToArray()))
                        {
                            if(area.AddAircraft(aircraft))
                            {
                                string time = DateTime.Now.ToString("HH:mm:ss.fff");
                                
                                AircraftData acd = AircraftDB.GetAircraftInfo(aircraft.ICAO);
                                string msg = $"[{time}] New Aircraft {aircraft.HexAddr} IN {area.AreaName} | {acd.Country} | Military : {acd.IsMilitary}";


                                AreaMonitorPopup.WriteLog(msg);  // Trace 대신 직접 호출
                            }
                            isInAnyArea = true;
                            break;
                        }
                        else
                        {
                            if (aircraft.Viewable)
                            {
                                if(area.ContainsAircraft(aircraft))
                                {
                                    string time = DateTime.Now.ToString("HH:mm:ss.fff");
                                    AircraftData acd = AircraftDB.GetAircraftInfo(aircraft.ICAO);
                                    string msg = $"[{time}] New Aircraft {aircraft.HexAddr} Out {area.AreaName} | {acd.Country} | Military : {acd.IsMilitary}";
                                    AreaMonitorPopup.WriteLog(msg);  // Trace 대신 직접 호출
                                    area.RemoveAircraft(aircraft);
                                }
                            }
                        }
                    }
                    aircraft.Viewable = isInAnyArea;
                }
            }
        }
    }


    public class TrackHookStruct
    {
        public long TimestampUtc { get; set; } // UTC timestamp in milliseconds
        public bool Valid_CC { get; set; } = false;
        public uint ICAO_CC { get; set; }
        public bool Valid_CPA { get; set; }
        public uint ICAO_CPA { get; set; }
        public Dictionary<string, string> DepartureAirport { get; set; }
        public Dictionary<string, string> ArrivalAirport { get; set; }
    }

    public class CPAConflictInfo
    {
        public uint ICAO1 { get; set; }
        public string HexAddr1 { get; set; }

        public double Lat1 { get; set; }
        public double Lon1 { get; set; }
        public double Alt1 { get; set; }

        public uint ICAO2 { get; set; }
        public string HexAddr2 { get; set; }

        public double Lat2 { get; set; }
        public double Lon2 { get; set; }
        public double Alt2 { get; set; }

        public double TCPA_Seconds { get; set; }
        public double CPADistance_NM { get; set; }

        public double Vertical_ft { get; set; }

        public String AreaName1 { get; set; }
        public String AreaName2 { get; set; }
    }

    public class Filter : IFilter<double>
    {
        public bool UseFilter { get; set; } = false;
        public double Min { get; set; } = 0.0;
        public double Max { get; set; } = 3000.0;
        
        public bool IsValid(double value)
        {
            if (!UseFilter)
                return true;
            if (value < Min || value > Max)
                return false;
            return true;
        }
    }

    public class TypeFilter : IFilter<string>
    {
        public bool UseFilter { get; set; } = false;
        public List<string> Types { get; set; } = new List<string>();
        public bool IsValid(string value)
        {
            if (!UseFilter)
                return true;
            if (string.IsNullOrEmpty(value))
                return false;
            return Types.Contains(value);
        }
    }

    public interface IFilter<T>
    {
        bool UseFilter { get; set; }
        bool IsValid(T value);
    }
}
