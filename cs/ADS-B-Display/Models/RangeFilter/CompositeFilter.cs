using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.RangeFilter
{
    public class CompositeFilter : IRangeFilter
    {
        private readonly List<IRangeFilter> _filters;

        public CompositeFilter(params IRangeFilter[] filters)
        {
            _filters = filters.ToList();
        }

        public bool IsWithinRange(double lat1, double lon1, double alt1, double lat2, double lon2, double alt2)
        {
            return _filters.All(f => f.IsWithinRange(lat1, lon1, alt1, lat2, lon2, alt2));
        }
    }
}
