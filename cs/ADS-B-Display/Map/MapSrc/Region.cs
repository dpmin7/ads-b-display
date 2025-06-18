using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    public class Region
    {
        public Vector3d[] V = new Vector3d[4]; // Virtual coordinates (used in rendering)
        public Vector2d[] W = new Vector2d[2]; // World coordinates (corners of parallel/meridian - oriented rectangle)
        public Vector3d[] P = new Vector3d[4]; // Virtual coordinates after projection to screen (in pixels)

        // Default constructor
        public Region()
        {
        }

        // Constructor with all projected coordinates given
        public Region(Vector3d v0, Vector3d v1, Vector3d v2, Vector3d v3, Vector2d w0, Vector2d w1)
        {
            V[0] = v0; V[1] = v1; V[2] = v2; V[3] = v3;
            W[0] = w0; W[1] = w1;
        }

        // Constructor with all coordinates given
        public Region(Vector3d v0, Vector3d v1, Vector3d v2, Vector3d v3, Vector2d w0, Vector2d w1,
                      Vector3d p0, Vector3d p1, Vector3d p2, Vector3d p3)
        {
            V[0] = v0; V[1] = v1; V[2] = v2; V[3] = v3;
            W[0] = w0; W[1] = w1;
            P[0] = p0; P[1] = p1; P[2] = p2; P[3] = p3;
        }

        // Reset Z-values of projected coords
        public void ResetProjZ()
        {
            for (int i = 0; i < 4; i++) {
                P[i].Z = 0.0f;
            }
        }

        // Length of edge (i,j)
        public double ProjLength(int i, int j)
        {
            double dx = P[i].X - P[j].X;
            double dy = P[i].Y - P[j].Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
