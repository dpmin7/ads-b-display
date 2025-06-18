using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    /// <summary>
    /// Manages TextureTile quadtree, loading and cache cleanup.
    /// Converted from TileManager.h/.cpp
    /// </summary>
    public class TileManager
    {
        private readonly ITileStorage _storage;
        private TextureTile _root;
        private int _textureCount;
        private const int DEFAULT_MAX_TEXTURES = 100;

        public TileManager(ITileStorage storage)
        {
            _storage = storage;
            _root = new TextureTile(0, 0, 0, null);
            _textureCount = 1;
        }

        /// <summary>
        /// Retrieves or creates a TextureTile at given coords and level, enqueues load.
        /// </summary>
        public TextureTile GetTexture(int x, int y, int level)
        {
            var cur = _root;
            for (int curLevel = 0; curLevel <= level; curLevel++) {
                int cx = x >> (level - curLevel);
                int cy = y >> (level - curLevel);
                int dx = cx & 1;
                int dy = cy & 1;
                var child = cur.GetChild(dx, dy);
                if (child == null) {
                    child = new TextureTile(cx, cy, curLevel, cur);
                    cur.SetChild(dx, dy, child);
                    _textureCount++;
                    _storage.Enqueue(child);
                }
                cur = child;
                cur.Touch();
            }
            return cur;
        }

        /// <summary>
        /// Cleanup one old leaf tile if over limit.
        /// </summary>
        public int Cleanup()
        {
            if (_textureCount <= DEFAULT_MAX_TEXTURES)
                return 0;
            var victim = FindTextureToDrop(_root, null);
            if (victim == null)
                return 0;
            var parent = victim.GetParent();
            for (int i = 0; i < 4; i++) {
                if (parent.GetChild(i) == victim) {
                    parent.SetChild(i, null);
                    victim.Unload();
                    _textureCount--;
                    return 1;
                }
            }
            return 0;
        }

        private TextureTile FindTextureToDrop(TextureTile cur, TextureTile best)
        {
            if (cur == null)
                return best;
            if (cur.GetParent() != null && cur.IsLeaf() && cur.IsOld) {
                if (best == null || cur.Level > best.Level)
                    best = cur;
                else if (cur.Level == best.Level && cur.GetAge() > best.GetAge())
                    best = cur;
            }
            for (int i = 0; i < 4; i++)
                best = FindTextureToDrop(cur.GetChild(i), best);
            return best;
        }
    }
}
