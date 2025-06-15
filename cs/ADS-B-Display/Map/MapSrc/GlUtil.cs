using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    /// <summary>
    /// OpenGL 유틸리티 함수 모음
    /// </summary>
    public static class GlUtil
    {
        /// <summary>
        /// 2D 투영 행렬을 설정합니다. 주어진 사각영역(x1,y1)-(x2,y2)를 화면에 매핑.
        /// </summary>
        public static void Projection2D(double x1, double y1, double x2, double y2)
        {
            double[] m = new double[]
            {
                2.0/(x2-x1), 0,            0, 0,
                0,            2.0/(y2-y1), 0, 0,
                0,            0,           1, 0,
                1.0+2.0*x2/(x1-x2), 1.0+2.0*y2/(y1-y2), 0, 1.0
            };
            GL.LoadMatrix(m);
        }

        /// <summary>
        /// 3D 투영(원근) 행렬을 설정합니다. fovy(시야각), 종횡비(aspect), 카메라 위치(x,y,z)를 이용.
        /// </summary>
        public static void Projection3D(double xRot, double yRot, double zTrans, double fovy, double aspect)
        {
            // 투영 행렬 설정
            GL.MatrixMode(MatrixMode.Projection);
            var projection = Matrix4.CreatePerspectiveFieldOfView(
                (float)(fovy), (float)aspect, 0.001f, 10f);
            GL.LoadMatrix(ref projection);

            // 모델뷰 행렬로 전환
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            // 카메라 위치 이동 및 회전
            GL.Translate(0, 0, (float)-zTrans);
            GL.Rotate((float)(-90.0 + yRot * 360.0), 1, 0, 0);
            GL.Rotate((float)(-xRot * 360.0), 0, 0, 1);
        }

        /// <summary>
        /// 화면에 문자열을 렌더링합니다. 현재 바인딩된 글꼴 텍스처를 사용해야 합니다.
        /// </summary>
        public static void RenderString(int x, int y, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            const int FONT_TEXTURE_SIZE = 256;
            const int CHAR_W = 8;
            const int CHAR_H = 14;

            GL.Begin(PrimitiveType.Quads);
            int cx = x;
            int cy = y;
            foreach (char ch in text) {
                if (ch == '\n') {
                    cx = x;
                    cy += CHAR_H;
                    continue;
                }
                int c = ch;
                float tx0 = (c % 16) * CHAR_W / (float)FONT_TEXTURE_SIZE;
                float ty0 = (c / 16) * CHAR_H / (float)FONT_TEXTURE_SIZE;
                float tx1 = tx0 + CHAR_W / (float)FONT_TEXTURE_SIZE;
                float ty1 = ty0 + CHAR_H / (float)FONT_TEXTURE_SIZE;

                GL.TexCoord2(tx0, ty0); GL.Vertex2(cx, cy);
                GL.TexCoord2(tx1, ty0); GL.Vertex2(cx + CHAR_W, cy);
                GL.TexCoord2(tx1, ty1); GL.Vertex2(cx + CHAR_W, cy + CHAR_H);
                GL.TexCoord2(tx0, ty1); GL.Vertex2(cx, cy + CHAR_H);

                cx += CHAR_W;
            }
            GL.End();
        }
    }
}
