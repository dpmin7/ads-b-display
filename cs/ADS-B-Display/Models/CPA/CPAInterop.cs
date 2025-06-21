using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace ADS_B_Display.Models.CPA
{
    internal static class CPAInterop
    {
        private const string DllName = "CPA_DLL.dll"; // DLL 파일명

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)] // C++ bool을 C# bool로 매핑
        public static extern bool computeCPA(
            double lat1, double lon1, double altitude1, double speed1, double heading1,
            double lat2, double lon2, double altitude2, double speed2, double heading2,
            out double tcpa, out double cpa_distance_nm, out double vertical_cpa);
    }

    
}