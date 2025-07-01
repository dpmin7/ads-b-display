using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.CPA
{
    internal class CPAInteropComputer : ICPAComputer
    {
        public bool ComputeCPA(Aircraft ac1, Aircraft ac2, out double tcpa, out double cpa_distance_nm, out double vertical_cpa)
        {
            // 유효성 검사 (선택 사항)
            if (ac1.Speed <= 0 || ac2.Speed <= 0 || ac1.Altitude < 0 || ac1.Altitude < 0)
            {
                tcpa = -1;
                cpa_distance_nm = -1;
                vertical_cpa = -1;
                return false;
            }

            return CPAInterop.computeCPA(
                ac1.Latitude, ac1.Longitude, ac1.Altitude, ac1.Speed, ac1.Heading,
                ac2.Latitude, ac2.Longitude, ac2.Altitude, ac2.Speed, ac2.Heading,
                out tcpa, out cpa_distance_nm, out vertical_cpa);
        }
    }
}
