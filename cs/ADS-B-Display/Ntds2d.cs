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

        // --- ✨ 텍스트 렌더링을 위해 새로 추가된 멤버 변수 ✨ ---
        private static Dictionary<string, (int textureId, int width, int height)> textTextureCache = new Dictionary<string, (int, int, int)>();

        // --- ✨ 텍스트 렌더링을 위한 헬퍼 함수 ✨ ---

        private static BitmapSource CreateTextBitmapWpf(string text, string fontFamily, double fontSize, System.Windows.Media.Color textColor)
        {
            var formattedText = new FormattedText(
                text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(fontFamily), fontSize, new SolidColorBrush(textColor), 1.0
            );

            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawText(formattedText, new Point(2, 0));
            }

            var bmp = new RenderTargetBitmap(
                (int)Math.Ceiling(formattedText.Width) + 4, (int)Math.Ceiling(formattedText.Height),
                96, 96, PixelFormats.Pbgra32
            );

            bmp.Render(drawingVisual);
            bmp.Freeze();
            return bmp;
        }

        private static (int textureId, int width, int height) CreateTextureFromBitmap(BitmapSource bitmap)
        {
            if (bitmap.Format != PixelFormats.Bgra32)
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            bitmap.CopyPixels(pixels, stride, 0);

            int textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                          OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, pixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return (textureId, width, height);
        }

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

        public static void DrawAirplaneImage(double x, double y, double h, double scale, double heading, int imageNum, bool isGhost)
        {
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

        public static void DrawTrackHook(double x, double y, double scale = 1)
        {
            GL.Color4(1f, 1f, 0f, 1f);
            GL.PushMatrix();
            GL.Translate(x, y, 0f);
            GL.Scale(scale, scale, 1f);
            GL.LineWidth((float)(1.5 * scale));
            GL.CallList(TrackHookList);
            GL.PopMatrix();
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
            GL.Color4(1.0f, 1.0f, 0.0f, 1.0f); // 노란색
            // 점1 원
            DrawCircleOutline(x1, y1, radius, segments);

            // 점2 원
            DrawCircleOutline(x2, y2, radius, segments);

            GL.Color4(1.0f, 0.0f, 0.0f, 1.0f);
            GL.LineWidth(2f * scale);
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
        public static void DrawAirportVBO(double x, double y, double scale, string text, System.Windows.Media.Color textColor, System.Windows.Media.Color? textBackgroundColor = null)
        {
            InitAirportVBO();
            if (string.IsNullOrEmpty(text))
            {
                // 텍스트가 없으면 기존 함수를 호출합니다.
                DrawAirportVBO(x, y, scale);
                return;
            }
            if (airportTextId == 0) return;

            // 텍스트 텍스처를 캐시에서 가져오거나 새로 생성합니다.
            // (참고: 텍스트 색상별로 캐싱하려면 키를 `(text, textColor)` 조합으로 변경해야 합니다.)
            if (!textTextureCache.TryGetValue(text, out var textTexture))
            {
                var textBitmap = CreateTextBitmapWpf(text, "Arial", 16, textColor);
                textTexture = CreateTextureFromBitmap(textBitmap);
                textTextureCache[text] = textTexture;
            }

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
            if (textTexture.textureId > 0)
            {
                // 텍스트 크기 및 위치 계산
                float textHeight = (float)(18f * scale); // 월드 좌표계 기준 텍스트 높이
                float textWidth = textHeight * textTexture.width / textTexture.height; // 종횡비 유지
                float iconTopY = (float)(24f * scale); // 아이콘의 최상단 Y 좌표
                float textMargin = (float)(5f * scale);  // 아이콘과 텍스트 사이 간격
                float textQuadX = -textWidth / 2.0f; // 텍스트를 수평 중앙에 위치
                float textQuadY = iconTopY + textMargin; // 텍스트의 하단 Y 좌표

                // --- 2a. 텍스트 배경 그리기 (색상이 지정된 경우) ---
                if (textBackgroundColor.HasValue)
                {
                    GL.Disable(EnableCap.Texture2D); // 텍스처 비활성화 (단색 사각형을 위함)
                    var bgColor = textBackgroundColor.Value;
                    GL.Color4(bgColor.R / 255.0f, bgColor.G / 255.0f, bgColor.B / 255.0f, bgColor.A / 255.0f);

                    float padding = (float)(3.0f * scale); // 배경 여백

                    GL.Begin(PrimitiveType.Quads);
                    GL.Vertex2(textQuadX - padding, textQuadY - padding);            // 좌하단
                    GL.Vertex2(textQuadX + textWidth + padding, textQuadY - padding);            // 우하단
                    GL.Vertex2(textQuadX + textWidth + padding, textQuadY + textHeight + padding); // 우상단
                    GL.Vertex2(textQuadX - padding, textQuadY + textHeight + padding); // 좌상단
                    GL.End();

                    GL.Enable(EnableCap.Texture2D); // 텍스트를 위해 텍스처 다시 활성화
                }

                // --- 2b. 텍스트 자체 그리기 ---
                GL.Color4(1.0f, 1.0f, 1.0f, 1.0f); // 텍스처 자체 색상을 사용하기 위해 흰색으로 설정
                GL.BindTexture(TextureTarget.Texture2D, textTexture.textureId);

                GL.Begin(PrimitiveType.Quads);
                GL.TexCoord2(0, 1); GL.Vertex2(textQuadX, textQuadY);            // 좌하단
                GL.TexCoord2(1, 1); GL.Vertex2(textQuadX + textWidth, textQuadY);            // 우하단
                GL.TexCoord2(1, 0); GL.Vertex2(textQuadX + textWidth, textQuadY + textHeight); // 우상단
                GL.TexCoord2(0, 0); GL.Vertex2(textQuadX, textQuadY + textHeight); // 좌상단
                GL.End();
            }

            // --- 정리 ---
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.Disable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Blend);
            GL.PopMatrix(); // 전체 이동 행렬 복원
        }

        public static void Draw2dCurve(double p1_x, double p1_y, double p2_x, double p2_y, double curvature, int segments)
        {
            GL.Color4(1.0f, 1.0f, 0.0f, 1.0f); // 노란색
            // 점1 원
            DrawCircleOutline(p1_x, p1_y, 20.0, 50);

            // 점2 원
            DrawCircleOutline(p2_x, p2_y, 20.0, 50);

            GL.Color4(1.0f, 1.0f, 1.0f, 1.0f); // 노란색

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
    }
}