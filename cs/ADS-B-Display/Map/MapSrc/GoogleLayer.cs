using ADS_B_Display;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ADS_B_Display.Map.MapSrc
{
    /// <summary>
    /// GoogleLayer: MasterLayer implementation fetching and rendering map tiles.
    /// Converted from GoogleLayer.h/.cpp
    /// </summary>
    public class GoogleLayer : MasterLayer
    {
        private readonly TileManager _tileManager;
        private const double MIN_TEXTURE_DISTANCE = 192.0;
        private const double EPSILON = 0.00001;

        public GoogleLayer(TileManager tileManager)
        {
            _tileManager = tileManager;
        }

        public override void RenderRegion(Region rgn)
        {
            // Determine maximal split level based on projected lengths
            int level = Math.Max(
                Math.Max(GetSplitLevel(rgn.W[1].X - rgn.W[0].X, rgn.ProjLength(0, 1)),
                         GetSplitLevel(rgn.W[1].X - rgn.W[0].X, rgn.ProjLength(2, 3))),
                Math.Max(GetSplitLevel(rgn.W[1].Y - rgn.W[0].Y, rgn.ProjLength(1, 2)),
                         GetSplitLevel(rgn.W[1].Y - rgn.W[0].Y, rgn.ProjLength(3, 0)))
            );

            double step = 1.0 / (1 << level);
            int numTiles = 1 << level;

            int x0 = (int)((rgn.W[0].X + 0.5) / step);
            int x1 = (int)(((rgn.W[1].X + 0.5) / step) - EPSILON);
            int y0 = (int)((rgn.W[0].Y + 0.5) / step);
            int y1 = (int)(((rgn.W[1].Y + 0.5) / step) - EPSILON);
            //Debug.WriteLine($"x1:{x1}, numTiles:{numTiles}");
            GL.Color3(1.0, 1.0, 1.0);
            //Debug.WriteLine($"Rendering region: x0={x0}, x1={x1}, y0={y0}, y1={y1}, level={level}");
            for (int x = x0-1; x <= x1+1; x++)
                for (int y = y0; y <= y1; y++) {
                    // Compute tile bounds in normalized region space
                    double tileX0 = x * step - 0.5;
                    double tileX1 = (x + 1) * step - 0.5;
                    double tileY0 = y * step - 0.5;
                    double tileY1 = (y + 1) * step - 0.5;

                    double textureX0 = 0.0, textureX1 = 1.0;
                    double textureY0 = 1.0, textureY1 = 0.0;

                    double xk0 = (tileX0 - rgn.W[0].X) / (rgn.W[1].X - rgn.W[0].X);
                    double xk1 = (tileX1 - rgn.W[0].X) / (rgn.W[1].X - rgn.W[0].X);
                    double yk0 = (tileY0 - rgn.W[0].Y) / (rgn.W[1].Y - rgn.W[0].Y);
                    double yk1 = (tileY1 - rgn.W[0].Y) / (rgn.W[1].Y - rgn.W[0].Y);

                    // Compute projected corner points
                    var p0 = Vector3d.Multiply(rgn.P[0], (float)((1 - xk0) * (1 - yk0))) +
                             Vector3d.Multiply(rgn.P[1], (float)(xk0 * (1 - yk0))) +
                             Vector3d.Multiply(rgn.P[2], (float)(xk0 * yk0)) +
                             Vector3d.Multiply(rgn.P[3], (float)((1 - xk0) * yk0));

                    var p1 = Vector3d.Multiply(rgn.P[0], (float)((1 - xk1) * (1 - yk0))) +
                             Vector3d.Multiply(rgn.P[1], (float)(xk1 * (1 - yk0))) +
                             Vector3d.Multiply(rgn.P[2], (float)(xk1 * yk0)) +
                             Vector3d.Multiply(rgn.P[3], (float)((1 - xk1) * yk0));

                    var p2 = Vector3d.Multiply(rgn.P[0], (float)((1 - xk1) * (1 - yk1))) +
                             Vector3d.Multiply(rgn.P[1], (float)(xk1 * (1 - yk1))) +
                             Vector3d.Multiply(rgn.P[2], (float)(xk1 * yk1)) +
                             Vector3d.Multiply(rgn.P[3], (float)((1 - xk1) * yk1));

                    var p3 = Vector3d.Multiply(rgn.P[0], (float)((1 - xk0) * (1 - yk1))) +
                             Vector3d.Multiply(rgn.P[1], (float)(xk0 * (1 - yk1))) +
                             Vector3d.Multiply(rgn.P[2], (float)(xk0 * yk1)) +
                             Vector3d.Multiply(rgn.P[3], (float)((1 - xk0) * yk1));

                    // Clip texture coordinates
                    if (xk0 < 0) textureX0 = -xk0 / (xk1 - xk0);
                    if (xk1 > 1) textureX1 = 1 - (xk1 - 1) / (xk1 - xk0);
                    if (yk0 < 0) textureY0 = 1 - (-yk0) / (yk1 - yk0);
                    if (yk1 > 1) textureY1 = (yk1 - 1) / (yk1 - yk0);

                    xk0 = MathExt.Clamp(xk0, 0.0, 1.0);
                    xk1 = MathExt.Clamp(xk1, 0.0, 1.0);
                    yk0 = MathExt.Clamp(yk0, 0.0, 1.0);
                    yk1 = MathExt.Clamp(yk1, 0.0, 1.0);

                    // Determine tile indices for LOD
                    int realLevel = level;
                    int realX = x % numTiles;
                    if (realX < 0) realX += numTiles;
                    int realY = y;

                    TextureTile tex = _tileManager.GetTexture(realX, realY, realLevel);

                    // Select best available texture
                    if (!tex.IsReady) {
                        TextureTile cur = null;
                        while (realLevel >= 0) {
                            cur = _tileManager.GetTexture(realX, realY, realLevel);
                            if (cur.IsReady) break;
                            textureX0 = ((realX & 1) + textureX0) / 2.0;
                            textureY0 = ((1 ^ (realY & 1)) + textureY0) / 2.0;
                            textureX1 = ((realX & 1) + textureX1) / 2.0;
                            textureY1 = ((1 ^ (realY & 1)) + textureY1) / 2.0;
                            realX >>= 1;
                            realY >>= 1;
                            realLevel--;
                        }
                        tex = cur;
                    }

                    if (tex.IsReady) {
                        GL.Enable(EnableCap.Texture2D);
                        tex.SetTexture();
                    } else {
                        GL.Disable(EnableCap.Texture2D);
                        GL.Color3(0.25f, 0.25f, 0.25f);
                    }

                    // Draw quad
                    GL.Begin(PrimitiveType.Quads);
                    GL.TexCoord2(textureX0, textureY0); GL.Vertex3(p0.X, p0.Y, p0.Z);
                    GL.TexCoord2(textureX1, textureY0); GL.Vertex3(p1.X, p1.Y, p1.Z);
                    GL.TexCoord2(textureX1, textureY1); GL.Vertex3(p2.X, p2.Y, p2.Z);
                    GL.TexCoord2(textureX0, textureY1); GL.Vertex3(p3.X, p3.Y, p3.Z);
                    GL.End();
                }

            // Render slave overdraw layers
            RenderOverdraw(rgn);
        }

        /// <summary>
        /// Calculate tile split level based on world/projected ratio.
        /// </summary>
        public int GetSplitLevel(double wlen, double plen)
        {
            int lvl = 0;
            while (true) {
                if (plen / wlen / (1 << (lvl + 1)) < MIN_TEXTURE_DISTANCE)
                    return lvl;
                lvl++;
            }
        }
    }
}