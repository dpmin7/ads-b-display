using OpenTK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            for (int i = 0; i < n; i++)
            {
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
        public static bool CheckComplex(Vector3d[] points, int numPoints)
        {
            for (int i = 0; i < numPoints; i++)
            {
                for (int j = i + 1; j < numPoints; j++)
                {
                    if (Math.Abs(i - j) <= 1 || (i == 0 && j == numPoints - 1))
                        continue;

                    var a1 = points[i];
                    var a2 = points[(i + 1) % numPoints];
                    var b1 = points[j];
                    var b2 = points[(j + 1) % numPoints];

                    if (LinesIntersect(a1, a2, b1, b2))
                        return true;
                }
            }
            return false;
        }

        public static TTriangles TriangulatePolygon(Vector3d[] points)
        {
            int n = points.Length;
            if (n < 3)
                return null;

            var vertices = points.ToList();
            var indices = Enumerable.Range(0, n).ToList();

            if (!IsCCW(vertices))
                indices.Reverse();

            List<(int, int, int)> triangleIndices = new List<(int, int, int)>();
            int count = 0;

            while (indices.Count > 3 && count++ < n * n)
            {
                bool earFound = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int i0 = indices[i % indices.Count];
                    int i1 = indices[(i + 1) % indices.Count];
                    int i2 = indices[(i + 2) % indices.Count];

                    if (IsEar(i0, i1, i2, vertices, indices))
                    {
                        triangleIndices.Add((i0, i1, i2));
                        indices.RemoveAt((i + 1) % indices.Count);
                        earFound = true;
                        break;
                    }
                }
                if (!earFound)
                    break;
            }

            if (indices.Count == 3)
                triangleIndices.Add((indices[0], indices[1], indices[2]));

            // 리스트를 TTriangles 연결 리스트로 변환
            TTriangles head = null;
            TTriangles current = null;
            foreach (var (i0, i1, i2) in triangleIndices)
            {
                var triangle = new TTriangles(i0, i1, i2);
                if (head == null)
                {
                    head = triangle;
                    current = triangle;
                }
                else
                {
                    current.Next = triangle;
                    current = triangle;
                }
            }

            return head;
        }

        private static bool LinesIntersect(Vector3d p1, Vector3d p2, Vector3d q1, Vector3d q2)
        {
            return CCW(p1, q1, q2) != CCW(p2, q1, q2) &&
                   CCW(p1, p2, q1) != CCW(p1, p2, q2);
        }

        private static bool CCW(Vector3d a, Vector3d b, Vector3d c)
        {
            return (c.Y - a.Y) * (b.X - a.X) > (b.Y - a.Y) * (c.X - a.X);
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


        public static void ReversePoints(Vector3d[] points, int numPoints)
        {
            int half = numPoints / 2;
            for (int i = 0; i < half; i++)
            {
                // swap points[i] <-> points[numPoints - 1 - i]
                var temp = points[i];
                points[i] = points[numPoints - 1 - i];
                points[numPoints - 1 - i] = temp;
            }
        }


        private static bool IsCCW(List<Vector3d> vertices)
        {
            return SignedArea(vertices) > 0;
        }

        private static double SignedArea(List<Vector3d> vertices)
        {
            double area = 0;
            int n = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                var v0 = vertices[i];
                var v1 = vertices[(i + 1) % n];
                area += (v0.X * v1.Y - v1.X * v0.Y);
            }
            return area / 2.0;
        }

        private static bool IsEar(int i0, int i1, int i2, List<Vector3d> vertices, List<int> indices)
        {
            var a = vertices[i0];
            var b = vertices[i1];
            var c = vertices[i2];
            if (!IsConvex(a, b, c))
                return false;

            foreach (int j in indices)
            {
                if (j == i0 || j == i1 || j == i2)
                    continue;
                if (PointInTriangle(vertices[j], a, b, c))
                    return false;
            }
            return true;
        }

        private static bool IsConvex(Vector3d a, Vector3d b, Vector3d c)
        {
            return ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) > 0;
        }

        private static bool PointInTriangle(Vector3d p, Vector3d a, Vector3d b, Vector3d c)
        {
            bool b1 = Sign(p, a, b) < 0.0;
            bool b2 = Sign(p, b, c) < 0.0;
            bool b3 = Sign(p, c, a) < 0.0;
            return (b1 == b2) && (b2 == b3);
        }

        private static double Sign(Vector3d p1, Vector3d p2, Vector3d p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }
    }
}

