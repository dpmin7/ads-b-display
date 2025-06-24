using ADS_B_Display;
using System;
using System.Collections.Generic;

namespace ADS_B_Display
{
    /// <summary>
    /// TADS_B_Aircraft 구조체를 C# 클래스로 변환 (char[] → string 적용)
    /// </summary>
    public class Aircraft
    {
        public uint ICAO { get; set; }
        public string HexAddr { get; set; } = new string('\0', 6); // 6자리 문자열
        public long LastSeen { get; set; }
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
        public double Altitude { get; set; }
        public bool HaveLatLon { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool HaveSpeedAndHeading { get; set; }
        public double Heading { get; set; }
        public double Speed { get; set; }
        public double VerticalRate { get; set; }
        public int SpriteImage { get; set; }
    }

    public struct TrackHookStruct
    {
        public bool Valid_CC { get; set; }
        public uint ICAO_CC { get; set; }
        public bool Valid_CPA { get; set; }
        public uint ICAO_CPA { get; set; }
        public Dictionary<string, string> DepartureAirport { get; set; }
        public Dictionary<string, string> ArrivalAirport { get; set; }
    }
}
