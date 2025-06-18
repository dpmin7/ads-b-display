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
        public static bool IsPointInArea((double lat, double lon) point, Area area)
        {
            if (area.NumPoints < 3) return false;

            var pts = area.Points;
            int n = area.NumPoints;

            // 평균 경도 계산 및 shift 결정
            double avgLon = 0;
            for (int i = 0; i < n; i++)
                avgLon += pts[i].X;
            avgLon /= n;

            double shift = (avgLon > 90) ? -360 : (avgLon < -90) ? 360 : 0;

            double px = point.lon + shift;
            double py = point.lat;

            bool inside = false;
            int j = n - 1;

            for (int i = 0; i < n; j = i++)
            {
                double xi = pts[i].X + shift;
                double yi = pts[i].Y;
                double xj = pts[j].X + shift;
                double yj = pts[j].Y;

                bool intersects = ((xi > px) != (xj > px));
                if (intersects)
                {
                    double invSlope = 1.0 / (xj - xi + 1e-12); // 역수 계산
                    double yCross = (yj - yi) * (px - xi) * invSlope + yi;
                    if (py < yCross)
                        inside = !inside;
                }
            }

            return inside;
        }
    }
}
