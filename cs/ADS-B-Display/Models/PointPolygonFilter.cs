using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models
{
    internal class PointPolygonFilter
    {
        /// <summary>
        /// Checks if the given point (latitude, longitude) is inside the specified polygonal area.
        /// </summary>
        /// <param name="point">Tuple of latitude and longitude.</param>
        /// <param name="area">Polygon area defined by a list of points.</param>
        /// <returns>True if the point is inside the area; otherwise, false.</returns>
        //public static bool IsPointInArea(double lat, double lon, Vector3d[] areaPoints)
        //{
        //    int n = areaPoints.Length;
        //    if (n < 3) return false;

        //    var pts = areaPoints;



        //    // 평균 경도 계산 및 shift 결정
        //    double avgLon = 0;
        //    for (int i = 0; i < n; i++)
        //        avgLon += pts[i].X;
        //    avgLon /= n;

        //    double shift = (avgLon > 90) ? -360 : (avgLon < -90) ? 360 : 0;

        //    double px = lon + shift;
        //    double py = lat;

        //    bool inside = false;
        //    int j = n - 1;

        //    for (int i = 0; i < n; j = i++)
        //    {
        //        double xi = pts[i].X + shift;
        //        double yi = pts[i].Y;
        //        double xj = pts[j].X + shift;
        //        double yj = pts[j].Y;

        //        bool intersects = ((xi > px) != (xj > px));
        //        if (intersects)
        //        {
        //            double invSlope = 1.0 / (xj - xi + 1e-12); // 역수 계산
        //            double yCross = (yj - yi) * (px - xi) * invSlope + yi;
        //            if (py < yCross)
        //                inside = !inside;
        //        }
        //    }

        //    return inside;
        //}

        //public static bool IsPointInArea(double lat, double lon, Vector3d[] areaPoints)
        //{
        //    int n = areaPoints.Length;
        //    if (n < 3) return false;

        //    // 평균 경도 대신 기준점을 검사 대상까지 포함하여 shift 기준 결정
        //    double[] allLons = new double[n + 1];
        //    for (int i = 0; i < n; i++)
        //        allLons[i] = areaPoints[i].X;
        //    allLons[n] = lon;

        //    double avgLon = allLons.Average();
        //    double shift = (avgLon > 90) ? -360 : (avgLon < -90) ? 360 : 0;

        //    double px = lon + shift;
        //    double py = lat;

        //    bool inside = false;
        //    int j = n - 1;
        //    for (int i = 0; i < n; j = i++)
        //    {
        //        double xi = areaPoints[i].X + shift;
        //        double yi = areaPoints[i].Y;
        //        double xj = areaPoints[j].X + shift;
        //        double yj = areaPoints[j].Y;

        //        bool intersects = ((xi > px) != (xj > px));
        //        if (intersects)
        //        {
        //            double invSlope = 1.0 / (xj - xi + 1e-12); // division by near-zero 방지
        //            double yCross = (yj - yi) * (px - xi) * invSlope + yi;
        //            if (py < yCross)
        //                inside = !inside;
        //        }
        //    }

        //    return inside;
        //}

        public static bool IsPointInArea(double lat, double lon, Vector3d[] areaPoints)
        {
            int n = areaPoints.Length;
            if (n < 3) return false;

            // 평균 경도 계산
            double avgLon = 0;
            for (int i = 0; i < n; i++)
                avgLon += areaPoints[i].X;
            avgLon /= n;

            // shift 계산 (360도 wrap-around 대응)
            double shift = 0;
            if (avgLon - lon > 180) shift = 360;
            else if (lon - avgLon > 180) shift = -360;

            double px = lon + shift;
            double py = lat;

            bool inside = false;
            int j = n - 1;

            for (int i = 0; i < n; j = i++)
            {
                double xi = areaPoints[i].X + shift;
                double yi = areaPoints[i].Y;
                double xj = areaPoints[j].X + shift;
                double yj = areaPoints[j].Y;

                bool intersects = ((yi > py) != (yj > py)) &&
                                  (px < (xj - xi) * (py - yi) / (yj - yi + 1e-12) + xi);
                if (intersects)
                    inside = !inside;
            }

            return inside;
        }
    }
}
