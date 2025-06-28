using ADS_B_Display;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;

namespace ADS_B_Display
{
    /// <summary>
    /// TADS_B_Aircraft 구조체를 C# 클래스로 변환 (char[] → string 적용)
    /// </summary>
    public class Aircraft
    {
        public uint ICAO { get; set; }
        public string HexAddr { get; set; } = new string('\0', 6); // 6자리 문자열
        public long LastSeen { get; set; } // 마지막으로 본 시간 (밀리초 단위)
        public long NumMessagesRaw { get; set; }
        public long NumMessagesSBS { get; set; }
        public int odd_cprlat { get; set; }
        public int odd_cprlon { get; set; }
        public int even_cprlat { get; set; }
        public int even_cprlon { get; set; }
        public long odd_cprtime { get; set; }
        public long even_cprtime { get; set; }
        public string FlightNum { get; set; } = string.Empty;  // Flight number을 string으로 처리
        public bool HaveFlightNum { get; set; }
        public bool HaveAltitude { get; set; }
        public bool HaveLatLon { get; set; }

        public double VLatitude { get; set; } // virtual Latitude for Animation
        private double _Latitude;
        public double Latitude {
            get => _Latitude;
            set {
                _Latitude = value;
                VLatitude = value; // virtual Latitude for Animation
            }
        }

        public double VLongitude { get; set; } // virtual Longitude for Animation
        private double _Longitude;
        public double Longitude {
            get => _Longitude;
            set {
                _Longitude = value;
                VLongitude = value; // virtual Latitude for Animation
            }
        }

        public double VAltitude { get; set; } // virtual Altitude for Animation
        private double _Altitude;
        public double Altitude {
            get => _Altitude;
            set {
                _Altitude = value;
                VAltitude = value; // virtual Latitude for Animation
            }
        }

        public TimedTrackQueue<AircraftTrackPoint> TrackPoint { get; set; } = new TimedTrackQueue<AircraftTrackPoint>(TimeSpan.FromSeconds(30)); // can remove after 30sec

        public bool HaveSpeedAndHeading { get; set; }
        public double Heading { get; set; }
        public double Speed { get; set; }
        public double VerticalRate { get; set; }
        public int SpriteImage { get; set; }
        public bool OnScreen { get; set; } = false; // 화면좌표에 존재하는지 여부
        public bool Viewable { get; set; } = false; // 특정 필터에 의해 표시 가능한지 여부
        public bool IsGhost { get; set; } = false; // Ghost 항공기 여부
        public bool UnRegistered { get; set; } = false; // 등록되지 않은 항공기 여부
        internal bool TimeCheck(long now, long ghostLimit, long purgeLimit)
        {
            // Purge 조건: 마지막으로 본 시간이 limit 이상이면 true
            if (now - LastSeen >= purgeLimit)
                return true; // Purge 대상

            if (now - LastSeen >= ghostLimit)
                IsGhost = true; // Ghost 항공기로 표시

            (VLatitude, VLongitude) = PredictPositionFlat(
               Latitude, Longitude,
                Heading,
                Speed, // 단위: m/s 또는 픽셀/s 등
                LastSeen,
                now);

            return false;
        }

        public static (double lat, double lon) PredictPositionFlat(
                    double lat0, double lon0,
                    double headingDeg,
                    double speedKnots,
                    long time0Ms, long timeNowMs)
        {
            double dt = (timeNowMs - time0Ms) / 1000.0;
            if (dt < 0 || dt > 10) return (lat0, lon0);

            double speedMps = speedKnots * 0.514444;
            double distance = speedMps * dt;

            double metersPerDegLat = 111000.0;
            double metersPerDegLon = 111000.0 * Math.Cos(lat0 * Math.PI / 180.0);

            double theta = (90.0 - headingDeg) * Math.PI / 180.0;
            double dLon = (distance * Math.Cos(theta)) / metersPerDegLon;
            double dLat = (distance * Math.Sin(theta)) / metersPerDegLat;

            return (lat0 + dLat, lon0 + dLon);
        }
    }

    public struct AircraftTrackPoint : ITimeIncluded
    {
        public double Latitude { get; }
        public double Longitude { get; }
        public double Altitude { get; }
        public long TimestampUtc { get; } // UTC timestamp in milliseconds

        public AircraftTrackPoint(double latitude, double longitude, double altitude, long timestampUtc)
        {
            Latitude = latitude;
            Longitude = longitude;
            Altitude = altitude;
            TimestampUtc = timestampUtc;
        }

        public override string ToString()
        {
            return $"({Latitude:F6}, {Longitude:F6}) @ {Altitude} ft - {TimestampUtc:O}";
        }
    }

    public struct TrackHookStruct
    {
        public long TimestampUtc { get; set; } // UTC timestamp in milliseconds
        public bool Valid_CC { get; set; }
        public uint ICAO_CC { get; set; }
        public bool Valid_CPA { get; set; }
        public uint ICAO_CPA { get; set; }
        public Dictionary<string, string> DepartureAirport { get; set; }
        public Dictionary<string, string> ArrivalAirport { get; set; }
    }

    public class TimedTrackQueue<T> where T : ITimeIncluded
    {
        private object _lock = new object();
        private readonly Queue<T> _queue = new Queue<T>();

        private long _historyDuration; // in milliseconds, how long to keep history entries
        public long HistoryDuration {
            get => _historyDuration;
            set {
                _historyDuration = value;
                //TrimOldEntries(TimeFunctions.GetCurrentTimeInMsec()); // 시간 바뀌면 즉시 정리
            }
        }

        public TimedTrackQueue(TimeSpan initialDuration)
        {
            _historyDuration = (long)initialDuration.TotalMilliseconds;
        }

        public void Enqueue(T point)
        {
            lock (_lock) {
                _queue.Enqueue(point);
                //TrimOldEntries(point.TimestampUtc);
            }
        }

        public void Clear()
        {
            lock (_lock) {
                _queue.Clear();
            }
        }

        private void TrimOldEntries(long currentTime)
        {
            while (_queue.Count > 0 && (currentTime - _queue.Peek().TimestampUtc) > _historyDuration) {
                _queue.Dequeue();
            }
        }

        public IList<T> Items {
            get {
                lock (_lock) {
                    long now = TimeFunctions.GetCurrentTimeInMsec();
                    long threshold = now - _historyDuration;

                    return _queue
                        //.Where(item => item.TimestampUtc >= threshold)
                        .ToList()
                        .AsReadOnly();
                }
            }
        }
    }

    public interface ITimeIncluded
    {
        long TimestampUtc { get; }
    }
}
