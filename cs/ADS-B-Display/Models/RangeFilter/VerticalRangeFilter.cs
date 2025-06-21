using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.RangeFilter
{
    public class VerticalRangeFilter : IRangeFilter
    {
        private readonly double _thresholdFt;

        public VerticalRangeFilter(double thresholdFt = 1000.0)
        {
            _thresholdFt = thresholdFt;
        }

        public bool IsWithinRange(double lat1, double lon1, double alt1, double lat2, double lon2, double alt2)
        {
            return Math.Abs(alt1 - alt2) <= _thresholdFt;
        }
    }
}
