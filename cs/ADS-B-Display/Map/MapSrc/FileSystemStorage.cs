using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    /// <summary>
    /// Filesystem cache to store tiles locally.
    /// Converted from FilesystemStorage.h/.cpp
    /// </summary>
    public class FileSystemStorage : SimpleTileStorage
    {
        private readonly string _storageRoot;
        private readonly bool _useGe;

        public FileSystemStorage(string root, bool useGe) : base()
        {
            _storageRoot = root;
            _useGe = useGe;
            Directory.CreateDirectory(_storageRoot);
        }

        protected override void Process(Tile tile)
        {
            if (!tile.IsLoaded) {
                // loading from disk
                string relPath = _useGe
                    ? PathFromCoordsGe(tile.X, tile.Y, tile.Level)
                    : PathFromCoordsNasa(tile.X, tile.Y, tile.Level);
                string fullPath = Path.Combine(_storageRoot, relPath);
                try {
                    if (!File.Exists(fullPath))
                        return;
                    byte[] data = File.ReadAllBytes(fullPath);
                    tile.Load(data, SaveStorage != null);
                } catch (IOException) {
                    // ignore load errors
                }
            } else if (tile.IsSaveable) {
                // saving to disk
                string relPath = _useGe
                    ? PathFromCoordsGe(tile.X, tile.Y, tile.Level)
                    : PathFromCoordsNasa(tile.X, tile.Y, tile.Level);
                string fullPath = Path.Combine(_storageRoot, relPath);
                string dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                byte[] raw = tile.ReleaseRawData();
                try {
                    File.WriteAllBytes(fullPath, raw);
                } catch (IOException) {
                    // on write error, remove partial
                    try { File.Delete(fullPath); } catch { }
                }
            }
        }

        private string PathFromCoordsGe(int x, int y, int level)
        {
            var path = new List<char>();
            var name = new List<char>();
            int deepness = 0;
            int tx = x, ty = y;
            for (int l = level; l >= 0; l--) {
                int middle = 1 << l;
                char c;
                if (tx < middle && ty < middle) c = '0';
                else if (tx >= middle && ty < middle) c = '1';
                else if (tx < middle && ty >= middle) c = '3';
                else c = '2';
                path.Add(c);
                name.Add(c);
                if (++deepness % 4 == 0)
                    path.Add(Path.DirectorySeparatorChar);
                tx %= middle;
                ty %= middle;
            }
            if (deepness % 4 != 0)
                path.Add(Path.DirectorySeparatorChar);
            name.AddRange(new[] { '.', 'j', 'p', 'g' });
            return new string(path.ToArray()) + new string(name.ToArray());
        }

        private string PathFromCoordsNasa(int x, int y, int level)
        {
            var path = new List<char>();
            var name = new List<char>();
            int tx = x, ty = y;
            for (int i = 0; i <= level; i++) {
                int bit = 1 << (level - i);
                char c;
                if ((tx & bit) != 0)
                    c = ((ty & bit) != 0) ? '2' : '1';
                else
                    c = ((ty & bit) != 0) ? '3' : '0';
                name.Add(c);
                if (i % 4 == 3)
                    path.Add(Path.DirectorySeparatorChar);
            }
            name.AddRange(new[] { '.', 'j', 'p', 'g' });
            return new string(path.ToArray()) + new string(name.ToArray());
        }
    }
}
