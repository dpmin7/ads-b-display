using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
//using System.Windows.Shapes;

namespace ADS_B_Display
{
    public static class TextRenderer2D
    {
        private static Dictionary<TextRenderKey, (int TextureId, int Width, int Height)> _textCache =
            new Dictionary<TextRenderKey, (int, int, int)>();
        private static readonly Font _defaultFont = new Font("Arial", 16, FontStyle.Regular);

        private static (int textureId, int width, int height) GetOrCreateTextTexture(string text, Color color)
        {
            var key = new TextRenderKey(text, color);
            if (_textCache.TryGetValue(key, out var cached))
                return cached;

            int width, height, textureId;
            using (var bmp = new Bitmap(256, 64))
            using (var g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                using (var brush = new SolidBrush(color)) {
                    g.DrawString(text, _defaultFont, brush, new PointF(0, 0));
                }

                width = bmp.Width;
                height = bmp.Height;

                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                        ImageLockMode.ReadOnly,
                                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, textureId);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                              data.Width, data.Height, 0,
                              OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                              PixelType.UnsignedByte, data.Scan0);
                bmp.UnlockBits(data);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            }

            _textCache[key] = (textureId, width, height);
            return (textureId, width, height);
        }

        public static void Draw2DText(this GLWpfControl control, string text, double x, double y, Color color)
        {
            var (textureId, width, height) = GetOrCreateTextTexture(text, color);

            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, textureId);

            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(0, 1); GL.Vertex2(x, y);
            GL.TexCoord2(1, 1); GL.Vertex2(x + width, y);
            GL.TexCoord2(1, 0); GL.Vertex2(x + width, y + height);
            GL.TexCoord2(0, 0); GL.Vertex2(x, y + height);
            GL.End();

            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.Disable(EnableCap.Texture2D);
        }

        public static void ClearCache()
        {
            foreach (var (texId, _, _) in _textCache.Values)
                GL.DeleteTexture(texId);
            _textCache.Clear();
        }
    }

    class TextRenderKey
    {
        public string Text { get; }
        public Color Color { get; }

        public TextRenderKey(string text, Color color)
        {
            Text = text;
            Color = color;
        }

        public override bool Equals(object obj)
        {
            var other = obj as TextRenderKey;
            if (other == null) return false;

            return Text == other.Text &&
                   Color.ToArgb() == other.Color.ToArgb(); // Color 비교
        }

        public override int GetHashCode()
        {
            unchecked {
                int hash = 17;
                hash = hash * 23 + Text.GetHashCode();
                hash = hash * 23 + Color.ToArgb().GetHashCode();
                return hash;
            }
        }
    }
}
