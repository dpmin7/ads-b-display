using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.RangeFilter
{
    public interface IRangeFilter
    {
        bool IsWithinRange(
            double lat1, double lon1, double alt1Ft,
            double lat2, double lon2, double alt2Ft);
    }
}
