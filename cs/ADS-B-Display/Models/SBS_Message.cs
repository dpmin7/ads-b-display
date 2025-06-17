using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Documents;

namespace AdsBDecoder
{
    /// <summary>
    /// SBS_Message 관련 상수 및 메서드를 C#으로 변환한 버전
    /// </summary>
    

    /// <summary>
    /// 전역 해시 테이블 처리를 흉내 내는 간단 유틸리티 (ght_get, ght_insert 대체)
    /// 실제 구현 시 자신의 자료구조를 사용하도록 수정하세요.
    /// </summary>
    //public static class GlobalHashTable
    //{
    //    private static readonly System.Collections.Generic.Dictionary<uint, Aircraft> _dict
    //        = new System.Collections.Generic.Dictionary<uint, Aircraft>();

    //    public static Aircraft GetOrAdd(uint icao)
    //    {
    //        if (!_dict.TryGetValue(icao, out var aircraft)) {
    //            aircraft = new Aircraft {
    //                ICAO = icao,
    //                HexAddr = icao.ToString("X6"),
    //                NumMessagesSBS = 0,
    //                NumMessagesRaw = 0,
    //                VerticalRate = 0,
    //                HaveAltitude = false,
    //                HaveLatLon = false,
    //                HaveSpeedAndHeading = false,
    //                HaveFlightNum = false,
    //                // SpriteImage 및 CycleImages 로직은 UI 쪽에서 설정
    //            };
    //            _dict[icao] = aircraft;
    //        }
    //        return aircraft;
    //    }

    //    public static bool TryGet(uint icao, out Aircraft aircraft)
    //    {
    //        return _dict.TryGetValue(icao, out aircraft);
    //    }

    //    public static IEnumerable<Aircraft> GetAll()
    //        => _dict.Values.ToList();
    //}

    /// <summary>
    /// TimeFunctions.GetCurrentTimeInMsec 대체
    /// </summary>
    public static class TimeFunctions
    {
        public static long GetCurrentTimeInMsec()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
