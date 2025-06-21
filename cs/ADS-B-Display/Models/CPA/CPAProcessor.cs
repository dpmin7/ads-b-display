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

        public static void CalculateAllCPA()
        {
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            var aircraftList = AircraftManager.GetAll();
            var aircraftArray = new List<Aircraft>(aircraftList);
            int count = aircraftArray.Count;

            Trace.WriteLine($"[CPA] Total aircrafts: {count}");

            int calculatedPairs = 0;
            int collisionCandidateCnt = 0;
            double thresholdForFilter = 85; 
            double thresholdForCPA_NM = 1;


            //IRangeFilter filter = new CompositeFilter(new HorizontalRangeFilter(180.0), new VerticalRangeFilter(1000.0));

            IRangeFilter filter = new HorizontalRangeFilter(thresholdForFilter);

            for (int i = 0; i < count; i++)
                {
                    for (int j = i + 1; j < count; j++)
                    {
                        var ac1 = aircraftArray[i];
                        var ac2 = aircraftArray[j];

                    if (!filter.IsWithinRange(ac1.Latitude, ac1.Longitude, ac1.Altitude, ac2.Latitude, ac2.Longitude, ac2.Altitude))
                    {
                        continue;
                    }

                    if (CPAInterop.computeCPA(
                            ac1.Latitude, ac1.Longitude, ac1.Altitude,
                            ac1.Speed, ac1.Heading,
                            ac2.Latitude, ac2.Longitude, ac2.Altitude,
                            ac2.Speed, ac2.Heading,
                            out double tcpa, out double cpaDistanceNm, out double verticalCpa))
                        {
                            if (cpaDistanceNm < thresholdForCPA_NM)
                            {
                                collisionCandidateCnt++;
                            }
                        }
                        //calculatedPairs++;
                    }

                }

#if DEBUG
            stopwatch.Stop();

                Trace.WriteLine($"[CPA] Total pairs calculated: {calculatedPairs:N0}");
                Trace.WriteLine($"[CPA] collisionCandidateCnt: {collisionCandidateCnt:N0}");
                Trace.WriteLine($"[CPA] Elapsed time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
#endif
            }

        private static bool HorizontalRangeFilter(double lat1, double lon1, double lat2, double lon2, double rangeNm)
        {
            const double degToNm = 60.0; // 1도 ≒ 60 해리 근사
            double dlat = lat1 - lat2;
            double dlon = lon1 - lon2;
            double distanceNm = Math.Sqrt(dlat * dlat + dlon * dlon) * degToNm;
            return distanceNm <= rangeNm;
        }

        private static bool VerticalRangeFilter(double altitude1Ft, double altitude2Ft, double thresholdFt = 1000.0)
        {
            return Math.Abs(altitude1Ft - altitude2Ft) <= thresholdFt;
        }

        private static bool Rough3DDistanceFilter(double lat1, double lon1, double alt1Ft, double lat2, double lon2, double alt2Ft, double thresholdNm)
        {
            const double degToNm = 60.0;         // 위도/경도 차이 1도 ≈ 60 해리
            const double feetToNm = 0.000164579; // 1피트 ≈ 0.000164579 해리

            double dLat = lat1 - lat2;
            double dLon = lon1 - lon2;
            double dAlt = (alt1Ft - alt2Ft) * feetToNm;

            double horizontalDistanceNm = Math.Sqrt(dLat * dLat + dLon * dLon) * degToNm;
            double totalDistanceNm = Math.Sqrt(horizontalDistanceNm * horizontalDistanceNm + dAlt * dAlt);

            return totalDistanceNm <= thresholdNm;
        }
    }
}
