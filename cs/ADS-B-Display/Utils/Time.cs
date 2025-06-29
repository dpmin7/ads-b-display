using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display
{
    /// <summary>
    /// TimeFunctions.GetCurrentTimeInMsec 대체
    /// </summary>
    public static class TimeFunctions
    {
        public static long GetCurrentTimeInMsec()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public static DateTime ConvertMsecToDateTime(long msec)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(msec).LocalDateTime;
        }
    }
}
