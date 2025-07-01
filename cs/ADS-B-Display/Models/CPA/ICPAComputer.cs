using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.CPA
{
    internal interface ICPAComputer
    {
        bool ComputeCPA(Aircraft ac1, Aircraft ac2, out double tcpa, out double cpa_distance_nm, out double vertical_cpa);
    }
}
