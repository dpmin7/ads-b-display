using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    public enum TileType
    {
        None = 0,
        Texture = 1
    }

    /// <summary>
    /// Base class for all earth data split into tiles.
    /// Converted from Tile.h/.cpp
    /// </summary>
    public abstract class Tile
    {
        private readonly object _lock = new object();
        private long _lastUsed;
        private byte[] _rawData;
        private bool _isNull;

        public int X { get; }
        public int Y { get; }
        public int Level { get; }

        protected Tile(int x, int y, int level)
        {
            X = x;
            Y = y;
            Level = level;
            Touch();
            _rawData = null;
            _isNull = false;
        }

        /// <summary>
        /// Updates last usage time.
        /// </summary>
        public void Touch()
        {
            _lastUsed = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Age in ticks since last Touch().
        /// </summary>
        public long GetAge()
        {
            return DateTime.UtcNow.Ticks - _lastUsed;
        }

        /// <summary>
        /// True if tile has not been used in last two frames (approx).
        /// </summary>
        public bool IsOld => GetAge() > TimeSpan.FromMilliseconds(16 * 2).Ticks;

        /// <summary>
        /// Type of tile; override in derived.
        /// </summary>
        public abstract TileType GetType();

        /// <summary>
        /// Load raw compressed data; keep indicates if to preserve for saving.
        /// </summary>
        public virtual void Load(byte[] data, bool keep)
        {
            if (keep)
                _rawData = data;
        }

        /// <summary>
        /// Release raw data buffer.
        /// </summary>
        public virtual byte[] ReleaseRawData()
        {
            var tmp = _rawData;
            _rawData = null;
            return tmp;
        }

        /// <summary>
        /// True if raw data exists for saving.
        /// </summary>
        public bool IsSaveable => _rawData != null;

        /// <summary>
        /// True if tile is loaded or marked null.
        /// </summary>
        public virtual bool IsLoaded => _isNull;

        /// <summary>
        /// True if tile is usable.
        /// </summary>
        public bool IsReady => !_isNull && IsLoaded;

        /// <summary>
        /// Mark this tile as not present.
        /// </summary>
        public virtual void Null()
        {
            _isNull = true;
            _rawData = null;
        }

        /// <summary>
        /// True if tile is explicitly marked null.
        /// </summary>
        public bool IsNull => _isNull;
    }
}
