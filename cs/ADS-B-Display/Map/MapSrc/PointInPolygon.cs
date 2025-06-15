using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display_NET.Map.MapSrc
{
    public static class PointInPolygon
    {
        // points: (x, y) 좌표 목록, pt: 검사할 (x, y)
        public static bool IsInside(IList<(double x, double y)> points, (double x, double y) pt)
        {
            bool inside = false;
            int numPoints = points.Count;
            for (int i = 0, j = numPoints - 1; i < numPoints; j = i++) {
                double xi = points[i].x, yi = points[i].y;
                double xj = points[j].x, yj = points[j].y;
                bool intersect = ((yi > pt.y) != (yj > pt.y)) &&
                                 (pt.x < (xj - xi) * (pt.y - yi) / (yj - yi) + xi);
                if (intersect)
                    inside = !inside;
            }
            return inside;
        }
    }
}
