using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    public static class DMS
    {
        public static string DegreesMinutesSecondsLat(double lat)
        {
            double absLat = Math.Abs(lat);
            int deg = (int)absLat;
            double minFloat = (absLat - deg) * 60;
            int min = (int)minFloat;
            double sec = (minFloat - min) * 60;
            char dir = (lat >= 0) ? 'N' : 'S';
            return $"{deg}°{min:00}'{sec:00.##}\"{dir}";
        }

        public static string DegreesMinutesSecondsLon(double lon)
        {
            double absLon = Math.Abs(lon);
            int deg = (int)absLon;
            double minFloat = (absLon - deg) * 60;
            int min = (int)minFloat;
            double sec = (minFloat - min) * 60;
            char dir = (lon >= 0) ? 'E' : 'W';
            return $"{deg}°{min:00}'{sec:00.##}\"{dir}";
        }
    }
}
