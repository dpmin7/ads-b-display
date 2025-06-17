using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ADS_B_Display
{
    public static class Dms
    {
        private const string DEG_SYMBOL = "°";

        public static string DegreesMinutesSeconds(double ang, int decimalPlaces = 2)
        {
            bool neg = false;
            if (ang < 0.0) {
                neg = true;
                ang = -ang;
            }

            int deg = (int)ang;
            double frac = ang - deg;
            frac *= 60.0;

            int min = (int)frac;
            frac -= min;

            double sec = Math.Round(frac * 60.0, decimalPlaces);

            if (sec >= 60.0) {
                min++;
                sec -= 60.0;
            }

            var sb = new StringBuilder();
            if (neg) sb.Append("-");

            sb.AppendFormat(CultureInfo.InvariantCulture, "{0}{1}{2:00}'{3:F" + decimalPlaces + "}\"", deg, DEG_SYMBOL, min, sec);
            return sb.ToString();
        }

        public static string DegreesMinutesSecondsLat(double ang, int decimalPlaces = 2)
        {
            string lat = DegreesMinutesSeconds(ang, decimalPlaces);

            if (lat.StartsWith("-")) {
                lat = lat.Substring(1) + " S";
            } else {
                lat += " N";
            }

            return " " + lat;
        }

        public static string DegreesMinutesSecondsLon(double ang, int decimalPlaces = 2)
        {
            string lon = DegreesMinutesSeconds(ang, decimalPlaces);

            if (lon.StartsWith("-")) {
                lon = lon.Substring(1) + " W";
            } else {
                lon += " E";
            }

            if (Math.Abs(ang) < 100.0) {
                lon = "0" + lon;
            }

            return lon;
        }

        public static double DecimalDegrees(string dms)
        {
            bool neg = dms.Contains("S") || dms.Contains("W") || dms.StartsWith("-");
            string clean = Regex.Replace(dms, @"[^\d.]+", " ").Trim();

            string[] parts = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) return 0;

            double deg = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double min = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 0.0;
            double sec = parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : 0.0;

            double result = deg + (min + sec / 60.0) / 60.0;
            return neg ? -result : result;
        }
    }
}
