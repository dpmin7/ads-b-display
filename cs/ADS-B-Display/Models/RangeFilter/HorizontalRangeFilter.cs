using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.RangeFilter
{
    public class HorizontalRangeFilter : IRangeFilter
    {
        private readonly double _thresholdNm;

        public HorizontalRangeFilter(double thresholdNm)
        {
            _thresholdNm = thresholdNm;
        }

        public bool IsWithinRange(double lat1, double lon1, double alt1, double lat2, double lon2, double alt2)
        {
            const double degToNm = 60.0;
            double dLat = lat1 - lat2;
            double dLon = lon1 - lon2;
            double distanceNm = Math.Sqrt(dLat * dLat + dLon * dLon) * degToNm;
            return distanceNm <= _thresholdNm;
        }
    }
}