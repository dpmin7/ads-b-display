using System;
using System.IO;
//using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenTK.Graphics.OpenGL;

namespace ADS_B_Display.Map.MapSrc
{
    /// <summary>
    /// Texture Container. Handles loading textures from files or memory and OpenGL binding.
    /// Uses WPF imaging instead of System.Drawing.
    /// </summary>
    public class Texture : IDisposable
    {
        private int _id;
        private byte[] _pixels;
        private int _width;
        private int _height;
        private PixelInternalFormat _internalFormat;
        private PixelFormat _format;

        public Texture()
        {
            _id = 0;
            _pixels = null;
        }

        public void Dispose()
        {
            Unload();
        }

        /// <summary>
        /// Bind texture to OpenGL, uploading pixel data if necessary.
        /// </summary>
        public void SetTexture()
        {
            if (_id == 0 && _pixels == null)
                throw new InvalidOperationException("Texture is empty");

            if (_id == 0) {
                _id = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _id);
                GL.TexImage2D(TextureTarget.Texture2D, 0, _internalFormat, _width, _height, 0, _format, PixelType.UnsignedByte, _pixels);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
                _pixels = null;
            } else {
                GL.BindTexture(TextureTarget.Texture2D, _id);
            }
        }

        public void Unload()
        {
            if (_id != 0) {
                GL.DeleteTexture(_id);
                _id = 0;
            }
        }

        /// <summary>
        /// Load texture from JPEG data in memory using WPF decoder.
        /// </summary>
        public void LoadJpeg(byte[] data)
        {
            using (var ms = new MemoryStream(data)) {
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.EndInit();
                image.Freeze(); // optional: make it cross-thread safe
                UploadBitmap(image);
            }
        }

        /// <summary>
        /// Load texture from PNG file using WPF decoder.
        /// </summary>
        public void LoadPng(string filePath)
        {
            var decoder = new PngBitmapDecoder(new Uri(filePath, UriKind.RelativeOrAbsolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            UploadBitmap(decoder.Frames[0]);
        }

        public void AdjustBrightness(float brightness)
        {
            if (_pixels == null)
            {
                // 아직 픽셀 데이터가 로드되지 않았을 수 있습니다.
                return;
            }

            // 밝기 값을 0.0과 1.0 사이로 제한합니다.
            brightness = Math.Max(0.0f, Math.Min(1.0f, brightness));

            // 밝기 조절이 필요 없는 경우는 연산을 건너뜁니다.
            if (brightness == 1.0f) return;

            // _format이 Bgra 이므로 픽셀당 4바이트(B, G, R, A) 입니다.
            for (int i = 0; i < _pixels.Length; i += 4)
            {
                // B, G, R 채널 값에만 밝기 값을 곱합니다. 알파(A) 채널은 투명도이므로 유지합니다.
                _pixels[i] = (byte)(_pixels[i] * brightness);     // Blue
                _pixels[i + 1] = (byte)(_pixels[i + 1] * brightness); // Green
                _pixels[i + 2] = (byte)(_pixels[i + 2] * brightness); // Red
                // _pixels[i + 3]은 알파(Alpha) 값이므로 변경하지 않습니다.
            }
        }
        private void UploadBitmap(BitmapSource bmp)
        {
            _width = bmp.PixelWidth;
            _height = bmp.PixelHeight;

            // Ensure format is BGRA32 for simplicity
            if (bmp.Format != System.Windows.Media.PixelFormats.Bgra32) {
                bmp = new FormatConvertedBitmap(bmp, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            }

            _internalFormat = PixelInternalFormat.Rgba;
            _format = PixelFormat.Bgra;

            int stride = (_width * bmp.Format.BitsPerPixel + 7) / 8;
            _pixels = new byte[stride * _height];
            bmp.CopyPixels(_pixels, stride, 0);
        }
    }
}
