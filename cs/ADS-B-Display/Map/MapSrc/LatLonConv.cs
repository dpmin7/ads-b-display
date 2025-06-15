using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display_NET.Map.MapSrc
{
    public static class LatLonConv
    {
        // 지도의 최대/최소 위도·경도와 화면 크기는 외부에서 설정할 수 있도록 프로퍼티로 노출
        public static double LatMin { get; set; } = -90;
        public static double LatMax { get; set; } = 90;
        public static double LonMin { get; set; } = -180;
        public static double LonMax { get; set; } = 180;
        public static double ScreenWidth { get; set; } = 800;   // 초기값, 컨트롤 크기에 따라 갱신
        public static double ScreenHeight { get; set; } = 600;  // 초기값

        public static void LatLonToXY(double lat, double lon, out double x, out double y)
        {
            x = (LonMax - lon) / (LonMax - LonMin) * ScreenWidth;
            y = (lat - LatMin) / (LatMax - LatMin) * ScreenHeight;
        }

        public static void XYToLatLon(double x, double y, out double lat, out double lon)
        {
            lon = LonMax - (x / ScreenWidth) * (LonMax - LonMin);
            lat = LatMin + ((ScreenHeight - y) / ScreenHeight) * (LatMax - LatMin);
        }
    }
}
