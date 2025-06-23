using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    /// <summary>
    /// TextureTile: Tile containing one image of earth surface.
    /// Implements quadtree structure and wraps a Texture for rendering.
    /// Converted from TextureTile.h/.cpp and aligned with new Tile base.
    /// </summary>
    public class TextureTile : Tile, IDisposable
    {
        private readonly TextureTile _parent;
        private readonly TextureTile[] _children = new TextureTile[4];
        private Texture _texture;

        public TextureTile(int x, int y, int level, TextureTile parent)
            : base(x, y, level)
        {
            _parent = parent;
            for (int i = 0; i < 4; i++)
                _children[i] = null;
        }

        /// <summary>
        /// Tile type identifier.
        /// </summary>
        public override TileType GetType() => TileType.Texture;

        /// <summary>
        /// Parent tile in quadtree.
        /// </summary>
        public TextureTile GetParent() => _parent;

        /// <summary>
        /// Child tile by index [0..3].
        /// </summary>
        public TextureTile GetChild(int n) => _children[n];

        /// <summary>
        /// Child tile by (x,y) bits.
        /// </summary>
        public TextureTile GetChild(int x, int y)
        {
            int idx = ((y & 1) << 1) | ((x ^ y) & 1);
            return _children[idx];
        }

        /// <summary>
        /// Set child tile by index.
        /// </summary>
        public void SetChild(int n, TextureTile child)
        {
            _children[n] = child;
        }

        /// <summary>
        /// Set child tile by (x,y).
        /// </summary>
        public void SetChild(int x, int y, TextureTile child)
        {
            int idx = ((y & 1) << 1) | ((x ^ y) & 1);
            _children[idx] = child;
        }

        /// <summary>
        /// True if no children.
        /// </summary>
        public bool IsLeaf()
        {
            foreach (var c in _children)
                if (c != null) return false;
            return true;
        }

        /// <summary>
        /// Load raw JPEG data into a Texture, then mark base tile.
        /// </summary>
        public override void Load(byte[] data, bool keep)
        {
            var tex = new Texture();
            try {
                tex.LoadJpeg(data);
            } catch {
                tex.Unload();
                throw;
            }
            _texture = tex;
            base.Load(data, keep);
        }

        /// <summary>
        /// True if texture is loaded or marked null.
        /// </summary>
        public override bool IsLoaded => IsNull || _texture != null;

        /// <summary>
        /// Bind the texture into OpenGL and update last-used.
        /// </summary>
        public void SetTexture()
        {
            Touch();
            if (IsReady)
                _texture.SetTexture();
        }

        /// <summary>
        /// Unload GPU resources.
        /// </summary>
        public void Unload()
        {
            _texture?.Unload();
            _texture = null;
        }

        public void Dispose()
        {
            _texture?.Unload();
        }
    }
}
