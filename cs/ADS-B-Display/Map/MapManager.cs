using ADS_B_Display.Map.MapSrc;
using ADS_B_Display.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map
{
    internal class MapManager
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetLogger("MapManager");

        private FileSystemStorage _storage;
        private KeyholeConnection _keyhole;
        private TileManager _tileManager;
        private MasterLayer _masterLayer;
        //private FlatEarthView _earthView; // 뷰는 뷰가
        private Action<MasterLayer> _loadMapCallback;

        private static readonly string[] Folders = new string[] {
            "GoogleMap",
            "VFR_Map",
            "IFR_Low_Map",
            "IFR_High_Map",
            "OpenStreetMap"
        };

        private static MapManager _instance = null;
        public static MapManager Instance => _instance ?? (_instance = new MapManager());

        private MapManager()
        {
            
        }

        private bool _loadMapFromInternet = true;

        public void RegisterLoadMapCallback(Action<MasterLayer> callback)
        {
            _loadMapCallback = callback;
        }

        public void LoadMap(TileServerType type)
        {
            // Determine base directory
            var homeDir = $"{Directory.GetCurrentDirectory()}\\Map";
            string subfolder;
            try {
                subfolder = Folders[(int)type];
            } catch(Exception ex) { logger.Error(ex.Message); throw new ArgumentOutOfRangeException(nameof(type)); }
            
            var cacheDir = Path.Combine(homeDir, subfolder);
            Directory.CreateDirectory(cacheDir);

            // Initialize filesystem storage
            _storage = new FileSystemStorage(cacheDir, useGe: true);

            // If using internet, chain keyhole connection
            if (_loadMapFromInternet) {
                _keyhole = new KeyholeConnection(type);
                _keyhole.SetSaveStorage(_storage);
                _storage.SetNextLoadStorage(_keyhole);
            }

            // TileManager and rendering layers
            _tileManager = new TileManager(_storage);
            _masterLayer = new GoogleLayer(_tileManager);
            //_earthView = new FlatEarthView(_masterLayer);

            // Resize view to current control size
            //_earthView.Resize((int)glControl.ActualWidth, (int)glControl.ActualHeight);

            _loadMapCallback?.Invoke(_masterLayer);
            //EventBus.Publish(EventIds.EvtMapLoaded, _masterLayer);
        }

        internal void ClearTitleManager()
        {
            _tileManager.Cleanup();
        }
    }
}
