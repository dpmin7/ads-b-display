using ADS_B_Display;
using System;

namespace AdsBDecoder
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

    public static class AircraftDecoder
    {
        private const double SMALL_VAL = 0.0001;
        private const int CPR_BITS = 131072; // 2^17
        private const uint MODES_NON_ICAO_ADDRESS = 1 << 24;

        private static int cprNLFunction(double lat)
        {
            lat = Math.Abs(lat);
            if (lat < 10.47047130) return 59;
            if (lat < 14.82817437) return 58;
            if (lat < 18.18626357) return 57;
            if (lat < 21.02939493) return 56;
            if (lat < 23.54504487) return 55;
            if (lat < 25.82924707) return 54;
            if (lat < 27.93898710) return 53;
            if (lat < 29.91135686) return 52;
            if (lat < 31.77209708) return 51;
            if (lat < 33.53993436) return 50;
            if (lat < 35.22899598) return 49;
            if (lat < 36.85025108) return 48;
            if (lat < 38.41241892) return 47;
            if (lat < 39.92256684) return 46;
            if (lat < 41.38651832) return 45;
            if (lat < 42.80914012) return 44;
            if (lat < 44.19454951) return 43;
            if (lat < 45.54626723) return 42;
            if (lat < 46.86733252) return 41;
            if (lat < 48.16039128) return 40;
            if (lat < 49.42776439) return 39;
            if (lat < 50.67150166) return 38;
            if (lat < 51.89342469) return 37;
            if (lat < 53.09516153) return 36;
            if (lat < 54.27817472) return 35;
            if (lat < 55.44378444) return 34;
            if (lat < 56.59318756) return 33;
            if (lat < 57.72747354) return 32;
            if (lat < 58.84763776) return 31;
            if (lat < 59.95459277) return 30;
            if (lat < 61.04917774) return 29;
            if (lat < 62.13216659) return 28;
            if (lat < 63.20427479) return 27;
            if (lat < 64.26616523) return 26;
            if (lat < 65.31845310) return 25;
            if (lat < 66.36171008) return 24;
            if (lat < 67.39646774) return 23;
            if (lat < 68.42322022) return 22;
            if (lat < 69.44242631) return 21;
            if (lat < 70.45451075) return 20;
            if (lat < 71.45986473) return 19;
            if (lat < 72.45884545) return 18;
            if (lat < 73.45177442) return 17;
            if (lat < 74.43893416) return 16;
            if (lat < 75.42056257) return 15;
            if (lat < 76.39684391) return 14;
            if (lat < 77.36789461) return 13;
            if (lat < 78.33374083) return 12;
            if (lat < 79.29428225) return 11;
            if (lat < 80.24923213) return 10;
            if (lat < 81.19801349) return 9;
            if (lat < 82.13956981) return 8;
            if (lat < 83.07199445) return 7;
            if (lat < 83.99173563) return 6;
            if (lat < 84.89166191) return 5;
            if (lat < 85.75541621) return 4;
            if (lat < 86.53536998) return 3;
            if (lat < 87.00000000) return 2;
            return 1;
        }

        private static int cprModFunction(int a, int b)
        {
            int res = a % b;
            if (res < 0) res += b;
            return res;
        }

        private static int cprNFunction(double lat, int isodd)
        {
            int nl = cprNLFunction(lat) - isodd;
            return nl < 1 ? 1 : nl;
        }

        private static double cprDlonFunction(double lat, int isodd)
        {
            return 360.0 / cprNFunction(lat, isodd);
        }

        private static void decodeCPR(Aircraft a)
        {
            const double AirDlat0 = 360.0 / 60.0;
            const double AirDlat1 = 360.0 / 59.0;

            double lat0 = a.even_cprlat;
            double lat1 = a.odd_cprlat;
            double lon0 = a.even_cprlon;
            double lon1 = a.odd_cprlon;

            int j = (int)Math.Floor(((59 * lat0 - 60 * lat1) / CPR_BITS) + 0.5);
            double rlat0 = AirDlat0 * (cprModFunction(j, 60) + lat0 / CPR_BITS);
            double rlat1 = AirDlat1 * (cprModFunction(j, 59) + lat1 / CPR_BITS);

            if (rlat0 >= 270.0) rlat0 -= 360.0;
            if (rlat1 >= 270.0) rlat1 -= 360.0;

            if (cprNLFunction(rlat0) != cprNLFunction(rlat1))
                return;

            if (a.even_cprtime > a.odd_cprtime) {
                int ni = cprNFunction(rlat0, 0);
                int m = (int)Math.Floor((((lon0 * (cprNLFunction(rlat0) - 1)) -
                                           (lon1 * cprNLFunction(rlat0))) / CPR_BITS) + 0.5);
                a.Longitude = cprDlonFunction(rlat0, 0) * (cprModFunction(m, ni) + lon0 / CPR_BITS);
                a.Latitude = rlat0;
            } else {
                int ni = cprNFunction(rlat1, 1);
                int m = (int)Math.Floor((((lon0 * (cprNLFunction(rlat1) - 1)) -
                                           (lon1 * cprNLFunction(rlat1))) / (double)CPR_BITS) + 0.5);
                a.Longitude = cprDlonFunction(rlat1, 1) * (cprModFunction(m, ni) + lon1 / CPR_BITS);
                a.Latitude = rlat1;
            }

            if (a.Longitude > 180.0)
                a.Longitude -= 360.0;
        }

        public static void RawToAircraft(ModeSMessage mm, ref Aircraft a)
        {
            long currentTime = TimeFunctions.GetCurrentTimeInMsec();
            a.LastSeen = currentTime;
            a.NumMessagesRaw++;

            if (mm.msg_type == 0 || mm.msg_type == 4 || mm.msg_type == 20) {
                a.Altitude = mm.altitude;
                a.HaveAltitude = true;
            } else if (mm.msg_type == 17) {
                if (mm.ME_type >= 1 && mm.ME_type <= 4) {
                    string flight = (mm.flight ?? string.Empty).PadRight(8).Substring(0, 8);
                    a.FlightNum = flight.TrimEnd();
                    a.HaveFlightNum = true;
                } else if (mm.ME_type >= 9 && mm.ME_type <= 18) {
                    a.Altitude = mm.altitude;
                    a.HaveAltitude = true;

                    if (mm.odd_flag != 0) {
                        a.odd_cprlat = mm.raw_latitude;
                        a.odd_cprlon = mm.raw_longitude;
                        a.odd_cprtime = currentTime;
                    } else {
                        a.even_cprlat = mm.raw_latitude;
                        a.even_cprlon = mm.raw_longitude;
                        a.even_cprtime = currentTime;
                    }

                    if (Math.Abs(a.even_cprtime - a.odd_cprtime) <= 10000) {
                        decodeCPR(a);
                        a.HaveLatLon = true;
                    }
                } else if (mm.ME_type == 19) {
                    if (mm.ME_subtype == 1 || mm.ME_subtype == 2) {
                        a.Speed = mm.velocity;
                        a.Heading = mm.heading;
                        a.VerticalRate = (mm.vert_rate_sign == 0 ? 1 : -1) * (mm.vert_rate - 1) * 64;
                        a.HaveSpeedAndHeading = true;
                    }
                }
            }
        }
    }
}
