using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display
{
    /// <summary>
    /// 다각형 삼각분할 및 유틸리티
    /// Converted from TriangulatPoly.h/.cpp
    /// </summary>
    public static class TrianglePoly
    {
        public const int CLOCKWISE = 1;
        public const int COUNTERCLOCKWISE = -1;

        /// <summary>
        /// 2D 다각형의 방향성 검사
        /// 반시계면 >0, 시계면 <0, 퇴화 =0
        /// </summary>
        public static int Orientation2DPolygon(double[][] V)
        {
            int n = V.Length;
            if (n < 3) return 0;
            int count = 0;
            for (int i = 0; i < n; i++) {
                int j = (i + 1) % n;
                int k = (i + 2) % n;
                double z = (V[j][0] - V[i][0]) * (V[k][1] - V[j][1])
                         - (V[j][1] - V[i][1]) * (V[k][0] - V[j][0]);
                if (z < 0) count--;
                else if (z > 0) count++;
            }
            if (count > 0) return COUNTERCLOCKWISE;
            if (count < 0) return CLOCKWISE;
            return 0;
        }

        /// <summary>
        /// 다각형의 복잡성 검사 (자기 교차 여부)
        /// </summary>
        public static bool CheckComplex(double[][] p)
        {
            int n = p.Length;
            // 모든 비인접 변 간 교차 검사
            for (int i = 0; i < n; i++)
                for (int j = i + 2; j < n; j++) {
                    if (i == 0 && j == n - 1) continue;
                    if (Intersect(p, n, i, j))
                        return true;
                }
            return false;
        }

        /// <summary>
        /// 단순 선분 교차 테스트
        /// </summary>
        private static bool Intersect(double[][] p, int n, int i1, int i2)
        {
            int s1 = i1 > 0 ? i1 - 1 : n - 1;
            int s2 = i2 > 0 ? i2 - 1 : n - 1;
            return Ccw(p[s1][0], p[s1][1], p[i1][0], p[i1][1], p[s2][0], p[s2][1])
                != Ccw(p[s1][0], p[s1][1], p[i1][0], p[i1][1], p[i2][0], p[i2][1])
                && Ccw(p[s2][0], p[s2][1], p[i2][0], p[i2][1], p[s1][0], p[s1][1])
                != Ccw(p[s2][0], p[s2][1], p[i2][0], p[i2][1], p[i1][0], p[i1][1]);
        }

        /// <summary>
        /// 세 점이 반시계인지 테스트
        /// </summary>
        private static bool Ccw(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            double dx1 = x2 - x1;
            double dy1 = y2 - y1;
            double dx2 = x3 - x2;
            double dy2 = y3 - y2;
            return dy1 * dx2 < dy2 * dx1;
        }

        /// <summary>
        /// 다각형을 삼각형으로 분해합니다. 간단한 팬 방식으로 처리.
        /// </summary>
        public static List<long[]> TriangulatePoly(double[][] verts)
        {
            int n = verts.Length;
            var tris = new List<long[]>();
            if (n < 3) return tris;
            // 단순 팬(fan) 방식: (0,i,i+1)
            for (int i = 1; i < n - 1; i++)
                tris.Add(new long[] { 0, i, i + 1 });
            return tris;
        }
    }
}
