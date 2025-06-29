using ADS_B_Display.Models.CPA;
using NLog;
using NLog.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.Windows.Markup;

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
        private static long _purgeLimitMS = 30000; // 30초 (1분) 후에 Purge
        private static long _ghostLimitMS = 10000; // 10초 (1분) 후에 Purge

        private static ObservableCollection<CPAConflictInfo> _cpaConflicts = new ObservableCollection<CPAConflictInfo>();
        public static ObservableCollection<CPAConflictInfo> CPAConflicts => _cpaConflicts;

        static AircraftManager()
        {
            // Timer 설정: 1초마다 데이터 업데이트
            _dataTimer = new Timer(300);
            _dataTimer.Elapsed += OnDataTimerElapsed;
            _dataTimer.AutoReset = true;
            _dataTimer.Enabled = true;

            //CollisionRiskWorker.Start();
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
                _cpaConflicts.Clear();
                foreach (var conflict in newConflicts)
                {
                    _cpaConflicts.Add(conflict);
                }
            }
        }

        internal static void SetPurgeLimitMS(long limitMS, long ghostLimitMS)
        {
            _purgeLimitMS = limitMS;
            _ghostLimitMS = ghostLimitMS;
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
                foreach (var kvp in _aircraftTable)
                {
                    if (kvp.Value.TimeCheck(now, _ghostLimitMS, _purgeLimitMS))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                // 두 번째 루프: Purge키로 aircraft 삭제
                foreach (var key in keysToRemove)
                {
                    _aircraftTable.Remove(key);
                }
            }

            UpdateViewableAircraftInPolygon();
        }

        internal static Aircraft GetOrAdd(uint icao)
        {
            if (!_aircraftTable.TryGetValue(icao, out var aircraft)) {
                aircraft = new Aircraft {
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
                lock (lockObj) {
                    _aircraftTable.Add(icao, aircraft);
                }
            }
            aircraft.AircraftData = AircraftDB.GetAircraftInfo(aircraft.ICAO);


            return aircraft;
        }

        internal static bool TryGet(uint icao, out Aircraft aircraft)
        {
            return _aircraftTable.TryGetValue(icao, out aircraft);
        }

        internal static IEnumerable<Aircraft> GetAll()
        {
            lock (lockObj) {
                return _aircraftTable.Values.ToList();
            }
        }

        internal static int Count()
        {
            lock (lockObj) {
                return _aircraftTable.Count;
            }
        }

        internal static uint ReceiveSBSMessage(string msgLine, long time)
        {
            // 필드를 분리
            string[] SBS_Fields = SBSMessage.SplitSbsFields(msgLine);
            if (SBSMessage.VerifySbsAndIcao(SBS_Fields, out uint addr) == false) {
                return 0; // 유효하지 않은 SBS 메시지
            }
            var aircraft = GetOrAdd(addr); // Aircraft 객체를 가져오거나 생성
            SBSMessage.SBS_Message_Decode(SBS_Fields, ref aircraft);

            return addr;
        }

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

        internal static uint ReceiveRawMessage(string msgLine, long time)
        {
            ModeSMessage modeSMessage = new ModeSMessage();
            var status = DecodeRawAdsB.Decode_RAW_message(msgLine, ref modeSMessage);

            if (status != TDecodeStatus.HaveMsg) {
                return 0; // 유효하지 않은 Raw 메시지
            } else if (status == TDecodeStatus.MsgHeartBeat) {
                return 0;
            }

            uint addr = (uint)((modeSMessage.AA[0] << 16) | (modeSMessage.AA[1] << 8) | modeSMessage.AA[2]);
            var aircraft = GetOrAdd(addr); // Aircraft 객체를 가져오거나 생성
            DecodeRawAdsB.RawToAircraft(modeSMessage, ref aircraft, time);

            return addr; // Raw 메시지 처리는 아직 구현되지 않음
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
                                string msg = $"[{time}] [Polygon] New Aircraft In Polygon: {aircraft.HexAddr} IN {area.AreaName}";

                                AreaMonitorPopup.WriteLog(msg);  // Trace 대신 직접 호출
                            }
                            isInAnyArea = true;
                            break;
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
    }
}
