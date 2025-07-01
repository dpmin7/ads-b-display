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
            for (int curLevel = 0; curLevel < level; curLevel++)
            {
                int cx = x >> (level - curLevel);
                int cy = y >> (level - curLevel);
                int dx = cx & 1;
                int dy = cy & 1;

                var child = cur.GetChild(dx, dy);
                if (child == null)
                {
                    child = new TextureTile(cx, cy, curLevel, cur);
                    cur.SetChild(dx, dy, child);
                    _textureCount++;
                    _storage.Enqueue(child);
                }
                cur = child;
            }

            int final_dx = x & 1;
            int final_dy = y & 1;
            var final_child = cur.GetChild(final_dx, final_dy);

            if (final_child == null)
            {
                final_child = new TextureTile(x, y, level, cur);
                cur.SetChild(final_dx, final_dy, final_child);
                _textureCount++;
                _storage.Enqueue(final_child);
            }
            // ✨ --- 핵심 수정 --- ✨
            // 타일이 존재하지만, 로딩에 실패한 상태(IsNull)이고 현재 로딩 중도 아니라면,
            // 다시 로딩을 시도하도록 큐에 추가합니다.
            else if (final_child.IsNull && !final_child.IsLoaded)
            {
                _storage.Enqueue(final_child);
            }

            final_child.Touch();
            return final_child;
        }

        /// <summary>
        /// Cleanup one old leaf tile if over limit.
        /// </summary>
        public int Cleanup()
        {
            int count = 0;
            while (_textureCount > DEFAULT_MAX_TEXTURES)
            {
                var victim = FindTextureToDrop(_root, null);
                if (victim == null)
                    break;

                var parent = victim.GetParent();
                for (int i = 0; i < 4; i++)
                {
                    if (parent.GetChild(i) == victim)
                    {
                        parent.SetChild(i, null);
                        victim.Unload(); // 이게 rawData 해제 핵심
                        _textureCount--;
                        count++;
                        break;
                    }
                }
            }
            return count;
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
