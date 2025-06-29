using ADS_B_Display.Models.RangeFilter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ADS_B_Display.Models.CPA
{
    internal class CPAProcessor
    {
        private const double EarthRadiusKm = 6371.0;
        private const double KmToNm = 0.539957;
        private const double DegToRad = Math.PI / 180.0;
        private static readonly NLog.Logger logger = NLog.LogManager.GetLogger("CPAProcessor");

        private readonly IRangeFilter _filter;
        private Stopwatch cpaStopwatch;
        private ICPAComputer cpaComputer;

        public CPAProcessor()
        {
            //_filter = new CompositeFilter(
            //new HorizontalRangeFilter(180.0),
            //new VerticalRangeFilter(1000.0));
            _filter = new HorizontalRangeFilter(180);

            //cpaComputer = new CPACsComputer();
            cpaComputer = new CPAInteropComputer();

            cpaStopwatch = new Stopwatch();
        }

        public void CalculateAllCPA()
        {
            Stopwatch cpaStopwatch = new Stopwatch();
            cpaStopwatch.Start();

            var aircraftList = AircraftManager.GetAll();
            var aircraftArray = aircraftList.ToArray(); // 불필요한 복사를 줄임
            int count = aircraftArray.Length;

            logger.Debug($"[CPA] Total aircrafts: {count}");
            int calculatedPairs = 0;
            int collisionCandidateCnt = 0;
            double thresholdForFilter = 85; //CPA 거리필터 임계값
            double thresholdForCPA_NM = 1;  //CPA 수평거리
            double thresholdForTcpa = 30;   //TCPA 시간
            List<CPAConflictInfo> conflictList = new List<CPAConflictInfo>(); // ⚠️ 임시 리스트

            Aircraft ac1 = null;
            Aircraft ac2 = null;
            for (int i = 0; i < count; i++)
            {
                ac1 = aircraftArray[i];
                if(!ac1.HaveAltitude)
                {
                    continue;
                }
                for (int j = i + 1; j < count; j++)
                {
                    ac2 = aircraftArray[j];
                    if (!ac2.HaveAltitude)
                    {
                        continue;
                    }

                    if (!_filter.IsWithinRange(ac1.Latitude, ac1.Longitude, ac1.Altitude, ac2.Latitude, ac2.Longitude, ac2.Altitude))
                    {
                        continue;
                    }

                    if (cpaComputer.ComputeCPA(ac1, ac2, out double tcpa, out double cpaDistanceNm))
                    {
                        if (tcpa > thresholdForTcpa)
                        {
                            continue;
                        }
                           
                        if (cpaDistanceNm < thresholdForCPA_NM)
                        {
                            CPAConflictInfo conflictInfo = new CPAConflictInfo();
                            conflictInfo.ICAO1 = ac1.ICAO;
                            conflictInfo.ICAO2 = ac2.ICAO;
                            conflictInfo.HexAddr1 = ac1.HexAddr;
                            conflictInfo.HexAddr2 = ac2.HexAddr;
                            conflictInfo.Lat1 = ac1.Latitude;
                            conflictInfo.Lat2 = ac2.Latitude;
                            conflictInfo.Lon1 = ac1.Longitude;
                            conflictInfo.Lon2 = ac2.Longitude; 
                            conflictInfo.Alt1 = ac1.Altitude;
                            conflictInfo.Alt2 = ac2.Altitude;  
                            conflictInfo.TCPA_Seconds = tcpa;
                            conflictInfo.CPADistance_NM = cpaDistanceNm;

                            conflictList.Add(conflictInfo);
                            collisionCandidateCnt++;
                        }
                    }
                    calculatedPairs++;
                }

            }

            AircraftManager.UpdateCPAConflicts(conflictList);
            cpaStopwatch.Stop();
             
            logger.Debug($"[CPA] Total aircrafts: {count}");
            logger.Debug($"[CPA] Total calculateCnt: {calculatedPairs:N0}");
            logger.Debug($"[CPA] Total candidateCnt: {collisionCandidateCnt:N0}");
            logger.Debug($"[CPA] Elapsed time: {cpaStopwatch.Elapsed.TotalSeconds:F2} seconds");
        }

        //private static bool HorizontalRangeFilter(double lat1, double lon1, double lat2, double lon2, double rangeNm)
        //{
        //    const double degToNm = 60.0; // 1도 ≒ 60 해리 근사
        //    double dlat = lat1 - lat2;
        //    double dlon = lon1 - lon2;
        //    double distanceNm = Math.Sqrt(dlat * dlat + dlon * dlon) * degToNm;
        //    return distanceNm <= rangeNm;
        //}

        //private static bool VerticalRangeFilter(double altitude1Ft, double altitude2Ft, double thresholdFt = 1000.0)
        //{
        //    return Math.Abs(altitude1Ft - altitude2Ft) <= thresholdFt;
        //}

        //private static bool Rough3DDistanceFilter(double lat1, double lon1, double alt1Ft, double lat2, double lon2, double alt2Ft, double thresholdNm)
        //{
        //    const double degToNm = 60.0;         // 위도/경도 차이 1도 ≈ 60 해리
        //    const double feetToNm = 0.000164579; // 1피트 ≈ 0.000164579 해리

        //    double dLat = lat1 - lat2;
        //    double dLon = lon1 - lon2;
        //    double dAlt = (alt1Ft - alt2Ft) * feetToNm;

        //    double horizontalDistanceNm = Math.Sqrt(dLat * dLat + dLon * dLon) * degToNm;
        //    double totalDistanceNm = Math.Sqrt(horizontalDistanceNm * horizontalDistanceNm + dAlt * dAlt);

        //    return totalDistanceNm <= thresholdNm;
        //}
    }
}
