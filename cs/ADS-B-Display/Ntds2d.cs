using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ADS_B_Display.Utils;
using SkiaSharp;
using System.Drawing.Imaging;
using System.Drawing;

// 참고: WPF 기능을 사용하므로, 프로젝트 파일(.csproj)에 <UseWPF>true</UseWPF> 설정과
// WindowsBase, PresentationCore 라이브러리 참조가 필요합니다.
namespace ADS_B_Display
{
    /// <summary>
    /// ADS-B Display용 2D 드로잉 유틸리티 모음 (ntds2d.h/.cpp 변환)
    /// </summary>
    public static class Ntds2d
    {
        // --- 기존 멤버 변수 ---
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
        private static int airportVbo = 0;
        private static int airportEbo = 0;
        private static int airportTextId = 0;
        private static bool airportVboInitialized = false;

        private static Dictionary<string, (int textureId, int width, int height)> textTextureCache = new Dictionary<string, (int, int, int)>();

        // font
        public struct FontChar
        {
            public int id;
            public float x, y, width, height;
            public float xOffset, yOffset, xAdvance;
        }

        public static Dictionary<char, FontChar> CharMap = new Dictionary<char, FontChar>();
        public static int fontTextureId;
        private static int _fontTextureWidth;
        private static int _fontTextureHeight;

        public static int MakeAirplaneImages()
        {
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
            for (int row = 0; row < 11; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    byte[] sub = new byte[SPRITE_WIDTH * SPRITE_HEIGHT * 4];
                    for (int y = 0; y < SPRITE_HEIGHT; y++)
                        for (int x = 0; x < SPRITE_WIDTH; x++)
                        {
                            int srcIndex = ((y + row * SPRITE_HEIGHT) * width + (x + col * SPRITE_WIDTH)) * 4;
                            int dstIndex = (y * SPRITE_WIDTH + x) * 4;
                            Array.Copy(sheetPixels, srcIndex, sub, dstIndex, 4);
                        }

                    int texId = TextureSprites[NumSprites];
                    GL.BindTexture(TextureTarget.Texture2D, texId);
                    GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, SPRITE_WIDTH, SPRITE_HEIGHT, 0,
                                  OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, sub);
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
            for (int i = 0; i <= 50; i++)
            {
                double angle = i * (double)Math.PI / 50f;
                GL.Vertex2(Math.Cos(angle) * 20f, Math.Sin(angle) * 20f);
            }
            GL.End();

            GL.Begin(PrimitiveType.Points);
            GL.Vertex2(0f, 0f);
            GL.End();
            GL.EndList();
        }

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
            for (int i = 0; i < 100; i++)
            {
                double angle = i * 2f * (double)Math.PI / 100f;
                GL.Vertex2(Math.Cos(angle) * 20f, Math.Sin(angle) * 20f);
            }
            GL.End();
            GL.Begin(PrimitiveType.LineStrip);
            for (int i = 0; i < 100; i++)
            {
                double angle = i * 2f * (double)Math.PI / 100f;
                GL.Vertex2(Math.Cos(angle) * 2f, Math.Sin(angle) * 2f);
            }
            GL.End();
            GL.EndList();
        }

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
            for (int i = 0; i < 100; i++)
            {
                double angle = i * 2f * (double)Math.PI / 100f;
                GL.Vertex2(Math.Cos(angle) * 60f, Math.Sin(angle) * 60f);
            }
            GL.End();
            GL.EndList();
        }

        public static void DrawAirplaneImage(double x, double y, double scale, double heading, int imageNum)
        {
            GL.PushMatrix();

            // ✨ 1. 블렌딩 활성화
            GL.Enable(EnableCap.Blend);
            // ✨ 2. 블렌딩 방식 설정 (가장 일반적인 알파 블렌딩)
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

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

            // ✨ 3. 다른 객체에 영향을 주지 않도록 블렌딩 비활성화
            GL.Disable(EnableCap.Blend);

            GL.PopMatrix();
        }

        public static void DrawAirplaneImage(double x, double y, double h, double scale, double heading, int imageNum, bool isGhost, bool isConflictRisk)
        {
            //if(isConflictRisk)
            //{
            //    GL.Color4(0f, 0, 0, 1f);
            //}
           if (!isGhost)
            {
                (double r, double g, double b) color = AltitudeToColor.GetAltitudeColorRGB(h);
                GL.Color4(color.r, color.g, color.b, 0.8f);
            }
            else
            {
                GL.Color4(0.5f, 0.5f, 0.5f, 0.8f);
            }
            DrawAirplaneImage(x, y, scale, heading, imageNum);
        }

        public static void DrawAirTrackFriend(double x, double y)
        {
            GL.PushMatrix(); GL.Translate(x, y, 0f); GL.CallList(AirTrackFriendList); GL.PopMatrix();
        }

        public static void DrawAirTrackHostile(double x, double y)
        {
            GL.PushMatrix(); GL.Translate(x, y, 0f); GL.CallList(AirTrackHostileList); GL.PopMatrix();
        }

        public static void DrawAirTrackUnknown(double x, double y)
        {
            GL.PushMatrix(); GL.Translate(x, y, 0f); GL.CallList(AirTrackUnknownList); GL.PopMatrix();
        }

        public static void DrawPoint(double x, double y)
        {
            GL.PushMatrix(); GL.Translate(x, y, 0f); GL.CallList(SurfaceTrackFriendList); GL.PopMatrix();
        }

        public static void DrawRadarCoverage(double xc, double yc, double major, double minor)
        {
            GL.Begin(PrimitiveType.TriangleFan);
            GL.Vertex2(xc, yc);
            for (int i = 0; i <= 360; i += 5)
            {
                double rad = (double)Math.PI * i / 180f;
                GL.Vertex2(xc + major * Math.Cos(rad), yc + minor * Math.Sin(rad));
            }
            GL.End();
        }

        public static void DrawLeader(double x1, double y1, double x2, double y2)
        {
            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex2(x1, y1);
            GL.Vertex2(x2, y2);
            GL.End();
        }

        public static void DrawLeader(double x1, double y1, double x2, double y2, double width)
        {
            GL.LineWidth((float)width);
            DrawLeader(x1, y1, x2, y2);
            GL.LineWidth(1f);
        }

        public static void DrawLeader(double x1, double y1, double x2, double y2, double width, (double r, double g, double b) color)
        {
            GL.Color4((float)color.r, (float)color.g, (float)color.b, 1f);
            DrawLeader(x1, y1, x2, y2, width);
            GL.Color4(1f, 1f, 1f, 1f);
        }

        public static void ComputeTimeToGoPosition(double timeToGo, double xs, double ys, double xv, double yv, out double xe, out double ye)
        {
            xe = xs + (xv / 3600f) * timeToGo;
            ye = ys + (yv / 3600f) * timeToGo;
        }

        public static void DrawLines(int resolution, double[] xpts, double[] ypts)
        {
            GL.Begin(PrimitiveType.Lines);
            for (int i = 0; i < resolution; i++)
            {
                GL.Vertex3(xpts[i], ypts[i], 0.1);
                GL.Vertex3(xpts[(i + 1) % resolution], ypts[(i + 1) % resolution], 0.1);
            }
            GL.End();
        }

        public static void DrawCirclesVBO(List<(double cx, double cy, double r)> circles, int segments = 12)
        {
            if (circleVbo == 0)
                GL.GenBuffers(1, out circleVbo);

            int vertsPerCircle = segments + 2;
            int totalVerts = circles.Count * vertsPerCircle;
            float[] vertexData = new float[totalVerts * 2];

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

            GL.Color4(1.0f, 1.0f, 1.0f, 1.0f);
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

        public static void DrawLinkedPointsWithCircles(double x1, double y1, double x2, double y2, float scale, double radius = 20.0, int segments = 50)
        {
            GL.LineWidth(2f * scale);
            GL.Color4(1.0f, 1.0f, 0.0f, 1.0f); // 노란색
            // 점1 원
            DrawCircleOutline(x1, y1, radius, segments);

            // 점2 원
            DrawCircleOutline(x2, y2, radius, segments);

            GL.Color4(1.0f, 0.0f, 0.0f, 1.0f);
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

            byte[] flippedPixels = new byte[height * stride];
            for (int y = 0; y < height; y++)
            {
                int srcIndex = y * stride;
                int dstIndex = (height - 1 - y) * stride;
                Array.Copy(pixels, srcIndex, flippedPixels, dstIndex, stride);
            }

            int textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureId);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                          OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, flippedPixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            return textureId;
        }

        // Ntds2d.cs 안에 있는 함수
        public static (int textureId, int width, int height) LoadTextureFontFromFile(string filePath)
        {
            // 1. 파일이 존재하는지 확인
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: Texture file not found at {filePath}");
                return (0, 0, 0);
            }

            // 2. WPF 비트맵 디코더로 이미지 로드
            var decoder = new PngBitmapDecoder(new Uri(filePath), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmap = decoder.Frames[0];

            // 3. 실제 이미지의 너비와 높이를 변수에 저장
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            // 4. 비트맵 픽셀 데이터를 byte 배열로 복사
            int stride = width * 4; // 픽셀당 4바이트(BGRA)
            byte[] pixels = new byte[height * stride];
            bitmap.CopyPixels(pixels, stride, 0);

            // 5. OpenGL 텍스처 생성
            int textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureId);

            // 6. 텍스처 데이터 업로드 (BGRA 형식으로)
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                          OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, pixels);

            // 7. ✨✨ 텍스처 파라미터 설정 (가장 중요) ✨✨

            // 필터링 모드: 주변 픽셀과 섞지 않고 가장 가까운 픽셀 하나만 선택 (글자를 선명하게)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            // 래핑 모드: 텍스처 좌표가 [0, 1] 범위를 벗어날 경우, 가장자리 픽셀을 사용 (경계선 깨짐 방지)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            // 8. 생성된 텍스처 ID와 실제 크기 반환
            return (textureId, width, height);
        }

        private static void InitAirportVBO()
        {
            if (airportVboInitialized) return;

            float[] quadVertices = {
                 // positions // texCoords
                 1f,  1f,   1f, 1f,
                -1f,  1f,   0f, 1f,
                -1f, -1f,   0f, 0f,
                 1f, -1f,   1f, 0f
            };
            uint[] indices = { 0, 1, 2, 2, 3, 0 };

            airportVbo = GL.GenBuffer();
            airportEbo = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ArrayBuffer, airportVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, airportEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            string texturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Symbols", "tower-64.png");
            if (File.Exists(texturePath))
            {
                airportTextId = LoadTextureFromFile(texturePath);
            }
            else
            {
                Console.WriteLine($"Error: Texture file not found at {texturePath}");
                airportTextId = 0;
            }
            airportVboInitialized = true;
        }

        public static void DrawAirportVBO(double x, double y, double scale)
        {
            // 이 버전은 이제 텍스트 없는 버전으로 남겨둡니다.
            InitAirportVBO();
            if (airportTextId == 0) return;

            GL.PushMatrix();
            GL.Translate(x, y, 0f);
            GL.Scale(24f * scale, 24f * scale, 1f);
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.BindTexture(TextureTarget.Texture2D, airportTextId);
            GL.Color4(1f, 1f, 1f, 1f);
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, airportVbo);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, airportEbo);
            int stride = 4 * sizeof(float);
            GL.VertexPointer(2, VertexPointerType.Float, stride, 0);
            GL.TexCoordPointer(2, TexCoordPointerType.Float, stride, (IntPtr)(2 * sizeof(float)));
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.TextureCoordArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.Disable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Blend);
            GL.PopMatrix();
        }

        /// <summary>
        /// VBO를 사용하여 공항 아이콘과 상단에 텍스트를 함께 그립니다.
        /// </summary>
        /// <param name="x">중심 X 좌표</param>
        /// <param name="y">중심 Y 좌표</param>
        /// <param name="scale">전체 크기 배율</param>
        /// <param name="text">아이콘 위에 표시할 텍스트</param>
        /// <param name="textColor">텍스트 색상</param>
        /// <param name="textBackgroundColor">텍스트 배경색 (기본값: 없음)</param>
        public static void DrawAirportVBO(double x, double y, double scale, string text)
        {
            InitAirportVBO();
            if (string.IsNullOrEmpty(text))
            {
                // 텍스트가 없으면 기존 함수를 호출합니다.
                DrawAirportVBO(x, y, scale);
                return;
            }
            if (airportTextId == 0) return;

            // --- 그리기 시작 ---
            GL.PushMatrix();
            GL.Translate(x, y, 0.0);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // --- 1. 공항 아이콘 그리기 ---
            GL.Enable(EnableCap.Texture2D);
            GL.Color4(1.0f, 1.0f, 1.0f, 1.0f);
            GL.PushMatrix();
            GL.Scale(24f * scale, 24f * scale, 1.0);
            GL.BindTexture(TextureTarget.Texture2D, airportTextId);

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);

            GL.BindBuffer(BufferTarget.ArrayBuffer, airportVbo);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, airportEbo);
            int stride = 4 * sizeof(float);
            GL.VertexPointer(2, VertexPointerType.Float, stride, 0);
            GL.TexCoordPointer(2, TexCoordPointerType.Float, stride, (IntPtr)(2 * sizeof(float)));
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.TextureCoordArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.PopMatrix(); // 아이콘 스케일 행렬 복원

            // --- 2. 텍스트 그리기 ---
            if (!string.IsNullOrEmpty(text))
            {
                float textHeight = (float)(18f * scale);
                float iconTopY = (float)(30f * scale);
                float textMargin = (float)(10f * scale);
                float textStartY = iconTopY + textMargin;

                // 텍스트의 전체 너비를 미리 측정
                float totalTextWidth = MeasureText(textHeight, text);
                // 중앙 정렬을 위한 시작 X 좌표 계산
                float textStartX = -totalTextWidth / 2.0f;
                // 배경 패딩 값
                float padding = 10.0f * (float)scale;

                // ✨ 모든 그리기 작업을 DrawText 함수에 위임
                DrawText(textStartX, textStartY, textHeight, text, padding);
            }

            // --- 정리 ---
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.Disable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Blend);
            GL.PopMatrix(); // 전체 이동 행렬 복원
        }

        public static void Draw2dCurve(double p1_x, double p1_y, double p2_x, double p2_y, double curvature, int segments, bool isLast)
        {
            var arrowSize = 30;

            GL.Color4(1.0f, 1.0f, 0.0f, 1.0f); // 노란색
            // 점1 원
            DrawCircleOutline(p1_x, p1_y, 20.0, 50);

            // 점2 원
            DrawCircleOutline(p2_x, p2_y, 20.0, 50);

            GL.Color4(1.0f, 1.0f, 1.0f, 1.0f); // 흰색

            // 1. 제어점(Control Point) 계산
            // 두 점의 중점을 구하고, 선분의 수직 방향으로 밀어내어 제어점을 만듭니다.
            double mid_x = (p1_x + p2_x) / 2;
            double mid_y = (p1_y + p2_y) / 2;

            double dx = p2_x - p1_x;
            double dy = p2_y - p1_y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            // 수직 벡터: (-dy, dx)
            double ctrl_x = mid_x - dy * curvature;
            double ctrl_y = mid_y + dx * curvature;

            // 2. 2차 베지에 곡선 그리기
            GL.Begin(PrimitiveType.LineStrip);
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                double a = (1 - t) * (1 - t);
                double b = 2 * t * (1 - t);
                double c = t * t;

                double x = a * p1_x + b * ctrl_x + c * p2_x;
                double y = a * p1_y + b * ctrl_y + c * p2_y;

                GL.Vertex2(x, y);
            }
            GL.End();

            // 2. 화살표 머리(삼각형) 계산 및 그리기
            double tangent_dx = p2_x - ctrl_x;
            double tangent_dy = p2_y - ctrl_y;
            double angle = Math.Atan2(tangent_dy, tangent_dx);

            GL.PushMatrix(); // 현재 행렬 상태 저장

            // 3. 좌표계의 원점을 선의 끝점으로 이동
            GL.Translate(p2_x, p2_y, 0);

            // 4. 선의 각도에 맞게 좌표계 회전 (라디안을 각도로 변환)
            GL.Rotate((float)(angle * 180 / Math.PI), 0, 0, 1);

            // 5. 회전된 좌표계 기준으로 표준화된 삼각형(화살표 머리) 그리기
            GL.Begin(PrimitiveType.Triangles);
            GL.Vertex2(0, 0); // 삼각형의 꼭짓점 (선의 끝점)
            GL.Vertex2(-arrowSize, -arrowSize / 2);
            GL.Vertex2(-arrowSize, arrowSize / 2);
            GL.End();

            GL.PopMatrix(); // 저장했던 행렬 상태 복원
        }

        public static void DisposeAllGLResources()
        {
            if (NumSprites > 0)
                GL.DeleteTextures(NumSprites, TextureSprites);
            NumSprites = 0;

            if (AirTrackFriendList != 0) GL.DeleteLists(AirTrackFriendList, 1);
            if (AirTrackHostileList != 0) GL.DeleteLists(AirTrackHostileList, 1);
            if (AirTrackUnknownList != 0) GL.DeleteLists(AirTrackUnknownList, 1);
            if (SurfaceTrackFriendList != 0) GL.DeleteLists(SurfaceTrackFriendList, 1);
            if (TrackHookList != 0) GL.DeleteLists(TrackHookList, 1);

            AirTrackFriendList = 0;
            AirTrackHostileList = 0;
            AirTrackUnknownList = 0;
            SurfaceTrackFriendList = 0;
            TrackHookList = 0;

            if (circleVbo != 0) GL.DeleteBuffer(circleVbo);
            if (airportVbo != 0) GL.DeleteBuffer(airportVbo);
            if (airportEbo != 0) GL.DeleteBuffer(airportEbo);
            if (airportTextId != 0) GL.DeleteTexture(airportTextId);

            circleVbo = 0;
            airportVbo = 0;
            airportEbo = 0;
            airportTextId = 0;

            // 캐시된 텍스트 텍스처들도 모두 해제합니다.
            if (textTextureCache.Count > 0)
            {
                GL.DeleteTextures(textTextureCache.Count, textTextureCache.Values.Select(t => t.textureId).ToArray());
                textTextureCache.Clear();
            }
        }

        public static void LoadFont(string fntPath, string pngPath)
        {
            CharMap.Clear();

            // 1. 텍스처 로드 (System.Drawing.Bitmap 사용)
            Bitmap bmp = new Bitmap(pngPath);
            fontTextureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, fontTextureId);

            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                data.Width, data.Height, 0,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra, // Bitmap은 BGRA 순서
                PixelType.UnsignedByte, data.Scan0);
            _fontTextureWidth = data.Width;
            _fontTextureHeight = data.Height;
            bmp.UnlockBits(data);
            bmp.Dispose(); // 리소스 해제

            // ✨ --- 수정된 텍스처 파라미터 --- ✨
            // 필터링 모드를 GL_NEAREST로 설정하여 글자를 선명하게
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.BindTexture(TextureTarget.Texture2D, 0);


            foreach (string line in File.ReadAllLines(fntPath))
            {
                if (!line.StartsWith("char id=")) continue;

                string[] parts = line.Split(' ');
                FontChar fc = new FontChar();
                foreach (var part in parts)
                {
                    var kv = part.Split('=');
                    if (kv.Length != 2) continue;
                    string key = kv[0];
                    string value = kv[1];

                    switch (key)
                    {
                        case "id": fc.id = int.Parse(value); break;
                        case "x": fc.x = float.Parse(value); break;
                        case "y": fc.y = float.Parse(value); break;
                        case "width": fc.width = float.Parse(value); break;
                        case "height": fc.height = float.Parse(value); break;
                        case "xoffset": fc.xOffset = float.Parse(value); break;
                        case "yoffset": fc.yOffset = float.Parse(value); break;
                        case "xadvance": fc.xAdvance = float.Parse(value); break;
                    }
                }

                if (fc.id > 0 && fc.id < 256)
                    CharMap[(char)fc.id] = fc;
            }
        }

        public static float MeasureText(float textHeight, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            float scale = textHeight / 32f; // 기본 폰트 기준 크기 (32px)
            float width = 0f;

            foreach (char c in text)
            {
                if (CharMap.TryGetValue(c, out var fc))
                {
                    width += fc.xAdvance * scale;
                }
            }

            return width;
        }

        /*
        public static void DrawText(float x, float y, float size, string text)
        {
            if (fontTextureId == 0 || string.IsNullOrEmpty(text)) return;

            // --- OpenGL 상태 설정 ---
            GL.Disable(EnableCap.Lighting);
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.BindTexture(TextureTarget.Texture2D, fontTextureId);
            GL.Color4(1f, 1f, 1f, 1f);

            // --- 그리기 시작 ---
            GL.Begin(PrimitiveType.Quads);

            float cursorX = x;
            float fontBaseSize = 32f; // .fnt 생성 시 사용한 폰트의 base size (예: 32)

            foreach (char c in text)
            {
                if (!CharMap.TryGetValue(c, out var fc)) continue;

                // 현재 원하는 글자 크기에 맞는 스케일 계산
                float scale = size / fontBaseSize;

                // 화면에 그려질 사각형의 크기와 위치
                float quadWidth = fc.width * scale;
                float quadHeight = fc.height * scale;
                float drawX = cursorX + (fc.xOffset * scale);
                float drawY = y - (fc.yOffset * scale); // Y축 방향이 반대이므로 뺌

                // 텍스처 UV 좌표
                float u1 = fc.x / _fontTextureWidth; // 텍스처 전체 너비 기준
                float v1 = fc.y / _fontTextureHeight; // 텍스처 전체 높이 기준
                float u2 = (fc.x + fc.width) / _fontTextureWidth;
                float v2 = (fc.y + fc.height) / _fontTextureHeight;

                // 정점과 텍스처 좌표 매핑 (Y축 반전 최종)
                // 화면의 위쪽(Vertex y)에 텍스처의 위쪽(TexCoord v)을 매핑하고, 화면 아래로 그림
                GL.TexCoord2(u1, v1); GL.Vertex2(drawX, drawY);
                GL.TexCoord2(u2, v1); GL.Vertex2(drawX + quadWidth, drawY);
                GL.TexCoord2(u2, v2); GL.Vertex2(drawX + quadWidth, drawY - quadHeight);
                GL.TexCoord2(u1, v2); GL.Vertex2(drawX, drawY - quadHeight);

                // 커서 이동
                cursorX += fc.xAdvance * scale;
            }

            GL.End();

            // --- 상태 복원 ---
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.Disable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Blend);
        }
        */

        public static void DrawText(float x, float y, float size, string text, float padding = 0)
        {
            if (fontTextureId == 0 || string.IsNullOrEmpty(text)) return;

            // --- 1. 텍스트의 전체 너비와 높이 미리 계산 ---
            float totalWidth = 0;
            float maxHeight = 0;
            float fontBaseSize = 32f; // .fnt 파일 생성 시 기준 크기
            float scale = size / fontBaseSize;

            foreach (char c in text)
            {
                if (CharMap.TryGetValue(c, out var fc))
                {
                    totalWidth += fc.xAdvance * scale;
                    if (fc.height * scale > maxHeight)
                    {
                        maxHeight = fc.height * scale;
                    }
                }
            }

            // --- 2. 배경 그리기 (색상이 지정된 경우) ---
            GL.Disable(EnableCap.Texture2D); // 텍스처 끄고 단색 사각형 그리기
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.Color4(0.0f, 0.0f, 0.0f, 1.0f);

            GL.Begin(PrimitiveType.Quads);
            GL.Vertex2(x - padding, y - padding);
            GL.Vertex2(x + totalWidth + padding, y - padding);
            GL.Vertex2(x + totalWidth + padding, y + size + padding);
            GL.Vertex2(x - padding, y + size + padding);
            GL.End();

            // --- 3. 텍스트 그리기 ---
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.BindTexture(TextureTarget.Texture2D, fontTextureId);
            GL.Color4(1.0f, 1.0f, 1.0f, 1.0f);

            GL.Begin(PrimitiveType.Quads);

            float cursorX = x;

            foreach (char c in text)
            {
                if (!CharMap.TryGetValue(c, out var fc)) continue;

                // 오프셋과 스케일을 적용한 최종 위치 및 크기 계산
                float drawX = cursorX + (fc.xOffset * scale);
                float drawY = y + (size - (fc.yOffset * scale));
                float quadWidth = fc.width * scale;
                float quadHeight = fc.height * scale;

                // UV 좌표
                float u1 = fc.x / _fontTextureWidth;
                float v1 = fc.y / _fontTextureHeight;
                float u2 = (fc.x + fc.width) / _fontTextureWidth;
                float v2 = (fc.y + fc.height) / _fontTextureHeight;

                // 정점과 텍스처 좌표 매핑
                GL.TexCoord2(u1, v2); GL.Vertex2(drawX, drawY - quadHeight);
                GL.TexCoord2(u2, v2); GL.Vertex2(drawX + quadWidth, drawY - quadHeight);
                GL.TexCoord2(u2, v1); GL.Vertex2(drawX + quadWidth, drawY);
                GL.TexCoord2(u1, v1); GL.Vertex2(drawX, drawY);

                cursorX += fc.xAdvance * scale;
            }

            GL.End();
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.Disable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Blend);
        }
    }
}