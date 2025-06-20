using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ADS_B_Display
{
    /// <summary>
    /// ADS-B Display용 2D 드로잉 유틸리티 모음 (ntds2d.h/.cpp 변환)
    /// </summary>
    public static class Ntds2d
    {
        private const int NUM_SPRITES = 81;
        private const int SPRITE_WIDTH = 72;
        private const int SPRITE_HEIGHT = 72;
        private static readonly int[] TextureSprites = new int[NUM_SPRITES];
        private static int NumSprites = 0;

        private static int AirTrackFriendList;
        private static int AirTrackHostileList;
        private static int AirTrackUnknownList;
        private static int SurfaceTrackFriendList;
        private static int TrackHookList;

        private static int circleVbo = 0;
        private static int circleCount = 0;
        private static int airportVbo = 0;
        private static int airportVao = 0;
        private static int airportEbo = 0;
        private static int airportTextId = 0;
        private static bool airportVboInitialized = false;

        /// <summary>
        /// 비행기 스프라이트 시트에서 개별 이미지를 분할하여 텍스처로 생성합니다.
        /// </summary>
        public static int MakeAirplaneImages()
        {
            // 스프라이트 시트 파일 경로
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Symbols", "sprites-RGBA.png");
            var decoder = new PngBitmapDecoder(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var sheet = decoder.Frames[0];
            int width = sheet.PixelWidth;
            int height = sheet.PixelHeight;
            int stride = (sheet.Format.BitsPerPixel / 8) * width;
            byte[] sheetPixels = new byte[stride * height];
            sheet.CopyPixels(sheetPixels, stride, 0);

            GL.GenTextures(NUM_SPRITES, TextureSprites);
            NumSprites = 0;
            for (int row = 0; row < 11; row++) {
                for (int col = 0; col < 8; col++) {
                    // 서브 이미지 버퍼
                    byte[] sub = new byte[SPRITE_WIDTH * SPRITE_HEIGHT * 4];
                    for (int y = 0; y < SPRITE_HEIGHT; y++)
                        for (int x = 0; x < SPRITE_WIDTH; x++) {
                            int srcIndex = ((y + row * SPRITE_HEIGHT) * width + (x + col * SPRITE_WIDTH)) * 4;
                            int dstIndex = (y * SPRITE_WIDTH + x) * 4;
                            Array.Copy(sheetPixels, srcIndex, sub, dstIndex, 4);
                        }

                    // 텍스처 생성
                    int texId = TextureSprites[NumSprites];
                    GL.BindTexture(TextureTarget.Texture2D, texId);
                    GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, SPRITE_WIDTH, SPRITE_HEIGHT, 0,
                                  PixelFormat.Rgba, PixelType.UnsignedByte, sub);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
                    GL.BindTexture(TextureTarget.Texture2D, 0);

                    NumSprites++;
                    if (NumSprites == NUM_SPRITES)
                        return NumSprites;
                }
            }
            return NumSprites;
        }

        /// <summary>
        /// 친구 항공기 트랙 심볼 GL display list 생성
        /// </summary>
        public static void MakeAirTrackFriend()
        {
            AirTrackFriendList = GL.GenLists(1);
            GL.NewList(AirTrackFriendList, ListMode.Compile);
            GL.PointSize(3f);
            GL.LineWidth(2f);
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.PointSmooth);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.Begin(PrimitiveType.LineStrip);
            for (int i = 0; i <= 50; i++) {
                double angle = i * (double)Math.PI / 50f;
                GL.Vertex2(Math.Cos(angle) * 20f, Math.Sin(angle) * 20f);
            }
            GL.End();

            GL.Begin(PrimitiveType.Points);
            GL.Vertex2(0f, 0f);
            GL.End();
            GL.EndList();
        }

        /// <summary>
        /// 적대 항공기 트랙 심볼 생성
        /// </summary>
        public static void MakeAirTrackHostile()
        {
            AirTrackHostileList = GL.GenLists(1);
            GL.NewList(AirTrackHostileList, ListMode.Compile);
            GL.PointSize(3f);
            GL.LineWidth(2f);
            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex2(-10f, 0f);
            GL.Vertex2(0f, 10f);
            GL.Vertex2(10f, 0f);
            GL.End();
            GL.Begin(PrimitiveType.Points);
            GL.Vertex2(0f, 0f);
            GL.End();
            GL.EndList();
        }

        /// <summary>
        /// 알 수 없음 트랙 심볼 생성
        /// </summary>
        public static void MakeAirTrackUnknown()
        {
            AirTrackUnknownList = GL.GenLists(1);
            GL.NewList(AirTrackUnknownList, ListMode.Compile);
            GL.PointSize(3f);
            GL.LineWidth(2f);
            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex2(-10f, 0f);
            GL.Vertex2(-10f, 10f);
            GL.Vertex2(10f, 10f);
            GL.Vertex2(10f, 0f);
            GL.End();
            GL.Begin(PrimitiveType.Points);
            GL.Vertex2(0f, 0f);
            GL.End();
            GL.EndList();
        }

        /// <summary>
        /// 포인트 심볼 생성
        /// </summary>
        public static void MakePoint()
        {
            SurfaceTrackFriendList = GL.GenLists(1);
            GL.NewList(SurfaceTrackFriendList, ListMode.Compile);
            GL.PointSize(3f);
            GL.LineWidth(3f);
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.PointSmooth);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.Begin(PrimitiveType.LineStrip);
            for (int i = 0; i < 100; i++) {
                double angle = i * 2f * (double)Math.PI / 100f;
                GL.Vertex2(Math.Cos(angle) * 20f, Math.Sin(angle) * 20f);
            }
            GL.End();
            GL.Begin(PrimitiveType.LineStrip);
            for (int i = 0; i < 100; i++) {
                double angle = i * 2f * (double)Math.PI / 100f;
                GL.Vertex2(Math.Cos(angle) * 2f, Math.Sin(angle) * 2f);
            }
            GL.End();
            GL.EndList();
        }

        /// <summary>
        /// 트랙 훅 심볼 생성
        /// </summary>
        public static void MakeTrackHook()
        {
            TrackHookList = GL.GenLists(1);
            GL.NewList(TrackHookList, ListMode.Compile);
            GL.PointSize(8f);
            GL.LineWidth(10f);
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.PointSmooth);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.Begin(PrimitiveType.LineStrip);
            for (int i = 0; i < 100; i++) {
                double angle = i * 2f * (double)Math.PI / 100f;
                GL.Vertex2(Math.Cos(angle) * 60f, Math.Sin(angle) * 60f);
            }
            GL.End();
            GL.EndList();
        }

        /// <summary>
        /// 비행기 이미지를 화면에 그리기
        /// </summary>
        public static void DrawAirplaneImage(double x, double y, double scale, double heading, int imageNum)
        {
            GL.PushMatrix();
            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, TextureSprites[imageNum]);
            GL.ShadeModel(ShadingModel.Flat);
            GL.Translate(x, y, 0f);
            GL.Rotate(-heading + 180, 0f, 0f, 1f);
            GL.Begin(PrimitiveType.Quads);
            double s = 36f * scale;
            GL.TexCoord2(1f, 1f); GL.Vertex2(s, s);
            GL.TexCoord2(0f, 1f); GL.Vertex2(-s, s);
            GL.TexCoord2(0f, 0f); GL.Vertex2(-s, -s);
            GL.TexCoord2(1f, 0f); GL.Vertex2(s, -s);
            GL.End();
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.Disable(EnableCap.Texture2D);
            GL.PopMatrix();
        }

        /// <summary>
        /// 친구 트랙 심볼 그리기
        /// </summary>
        public static void DrawAirTrackFriend(double x, double y)
        {
            GL.PushMatrix(); GL.Translate(x, y, 0f); GL.CallList(AirTrackFriendList); GL.PopMatrix();
        }

        /// <summary>
        /// 적대 트랙 심볼 그리기
        /// </summary>
        public static void DrawAirTrackHostile(double x, double y)
        {
            GL.PushMatrix(); GL.Translate(x, y, 0f); GL.CallList(AirTrackHostileList); GL.PopMatrix();
        }

        /// <summary>
        /// 알 수 없음 트랙 심볼 그리기
        /// </summary>
        public static void DrawAirTrackUnknown(double x, double y)
        {
            GL.PushMatrix(); GL.Translate(x, y, 0f); GL.CallList(AirTrackUnknownList); GL.PopMatrix();
        }

        /// <summary>
        /// 점 심볼 그리기
        /// </summary>
        public static void DrawPoint(double x, double y)
        {
            GL.PushMatrix(); GL.Translate(x, y, 0f); GL.CallList(SurfaceTrackFriendList); GL.PopMatrix();
        }

        /// <summary>
        /// 트랙 훅 심볼 그리기
        /// </summary>
        public static void DrawTrackHook(double x, double y, double scale = 1)
        {
            GL.PushMatrix();
            GL.Translate(x, y, 0f);
            GL.Scale(scale, scale, 1f); // 크기 조절 추가
            GL.LineWidth((float)(1.5 * scale)); // 기본 두께 1.5를 scale에 따라 조절
            GL.CallList(TrackHookList);
            GL.PopMatrix();
        }

        /// <summary>
        /// 레이더 커버리지 영역 그리기 (타원형)
        /// </summary>
        public static void DrawRadarCoverage(double xc, double yc, double major, double minor)
        {
            GL.Begin(PrimitiveType.TriangleFan);
            GL.Vertex2(xc, yc);
            for (int i = 0; i <= 360; i += 5) {
                double rad = (double)Math.PI * i / 180f;
                GL.Vertex2(xc + major * Math.Cos(rad), yc + minor * Math.Sin(rad));
            }
            GL.End();
        }

        /// <summary>
        /// 선 렌더링 (두 점 연결)
        /// </summary>
        public static void DrawLeader(double x1, double y1, double x2, double y2)
        {
            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex2(x1, y1);
            GL.Vertex2(x2, y2);
            GL.End();
        }

        /// <summary>
        /// 목적지 점 계산
        /// </summary>
        public static void ComputeTimeToGoPosition(double timeToGo, double xs, double ys, double xv, double yv, out double xe, out double ye)
        {
            xe = xs + (xv / 3600f) * timeToGo;
            ye = ys + (yv / 3600f) * timeToGo;
        }

        /// <summary>
        /// 다각형 형태의 선 그리기
        /// </summary>
        public static void DrawLines(int resolution, double[] xpts, double[] ypts)
        {
            GL.Begin(PrimitiveType.Lines);
            for (int i = 0; i < resolution; i++) {
                GL.Vertex3(xpts[i], ypts[i], 0.1);
                GL.Vertex3(xpts[(i + 1) % resolution], ypts[(i + 1) % resolution], 0.1);
            }
            GL.End();
        }

        /// <summary>
        /// 여러 개의 원을 VBO를 사용해 그리기
        /// </summary>
        public static void DrawCirclesVBO(List<(double cx, double cy, double r)> circles, int segments = 12)
        {
            if (circleVbo == 0)
                GL.GenBuffers(1, out circleVbo);

            int vertsPerCircle = segments + 2; // 중심점 + segments + 끝점
            int totalVerts = circles.Count * vertsPerCircle;
            float[] vertexData = new float[totalVerts * 2]; // (x, y)

            int index = 0;
            foreach (var (cx, cy, r) in circles)
            {
                vertexData[index++] = (float)cx;
                vertexData[index++] = (float)cy;

                for (int i = 0; i <= segments; i++)
                {
                    double angle = 2.0 * Math.PI * i / segments;
                    float x = (float)(cx + r * Math.Cos(angle));
                    float y = (float)(cy + r * Math.Sin(angle));
                    vertexData[index++] = x;
                    vertexData[index++] = y;
                }
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, circleVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexData.Length * sizeof(float), vertexData, BufferUsageHint.DynamicDraw);

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.VertexPointer(2, VertexPointerType.Float, 0, IntPtr.Zero);

            GL.Color4(1.0f, 1.0f, 1.0f, 1.0f); // 흰색
            for (int i = 0; i < circles.Count; i++)
            {
                GL.DrawArrays(PrimitiveType.TriangleFan, i * vertsPerCircle, vertsPerCircle);
            }

            GL.DisableClientState(ArrayCap.VertexArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        public static void DrawCircleOutline(double cx, double cy, double r, int segments = 64)
        {
            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                double x = cx + r * Math.Cos(angle);
                double y = cy + r * Math.Sin(angle);
                GL.Vertex2(x, y);
            }
            GL.End();
        }

        public static void DrawLinkedPointsWithCircles(double x1, double y1, double x2, double y2, double radius = 10.0, int segments = 12)
        {
            GL.Color4(0.0f, 0.0f, 0.0f, 1.0f); // 검은색
            // 점1 원
            DrawCircleOutline(x1, y1, radius, segments);

            // 점2 원
            DrawCircleOutline(x2, y2, radius, segments);
            GL.Color4(1.0f, 0.0f, 0.0f, 1.0f); // 빨간색
            // 선 연결
            GL.Begin(PrimitiveType.Lines);
            GL.Vertex2(x1, y1);
            GL.Vertex2(x2, y2);
            GL.End();
        }

        public static int LoadTextureFromFile(string filePath)
        {
            var decoder = new PngBitmapDecoder(new Uri(filePath), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmap = decoder.Frames[0];
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            int stride = width * 4;

            byte[] pixels = new byte[height * stride];
            bitmap.CopyPixels(pixels, stride, 0);

            int textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                          PixelFormat.Bgra, PixelType.UnsignedByte, pixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            return textureId;
        }
        private static void InitAirportVBO()
        {
            if (airportVboInitialized) return;

            float[] quadVertices = {
        // x, y, u, v
        +1f, +1f, 1f, 1f,
        -1f, +1f, 0f, 1f,
        -1f, -1f, 0f, 0f,
        +1f, -1f, 1f, 0f
    };

            uint[] indices = { 0, 1, 2, 0, 2, 3 };

            airportVao = GL.GenVertexArray();
            airportVbo = GL.GenBuffer();
            airportEbo = GL.GenBuffer();

            GL.BindVertexArray(airportVao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, airportVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, airportEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0); // vertex.xy
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

            GL.EnableVertexAttribArray(1); // texcoord.uv
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            GL.BindVertexArray(0);

            airportTextId = LoadTextureFromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Symbols", "tower-64.png"));
            airportVboInitialized = true;
        }

        public static void DrawAirportVBO(double x, double y, double scale)
        {
            InitAirportVBO(); // 텍스처는 여기서 이미 로딩되어 있음

            GL.PushMatrix();
            GL.Translate(x, y, 0f);
            GL.Scale(24f * scale, 24f * scale, 1f);

            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, airportTextId);

            // VAO/VBO 제거하고 고전 방식으로 그리기
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(1f, 0f); GL.Vertex2(+1f, +1f);
            GL.TexCoord2(0f, 0f); GL.Vertex2(-1f, +1f);
            GL.TexCoord2(0f, 1f); GL.Vertex2(-1f, -1f);
            GL.TexCoord2(1f, 1f); GL.Vertex2(+1f, -1f);
            GL.End();

            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.Disable(EnableCap.Texture2D);
            GL.PopMatrix();
        }
    }
}
