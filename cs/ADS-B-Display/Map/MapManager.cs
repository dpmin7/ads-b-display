using ADS_B_Display.Map.MapSrc;
using System;
using System.Collections.Generic;
using System.IO;

namespace ADS_B_Display.Map
{
    public enum TileServerType
    {
        GoogleMaps = 0,
        SkyVector_VFR = 1,
        SkyVector_IFR_Low = 2,
        SkyVector_IFR_High = 3,
        OpenStreet = 4,
    }
    internal class MapManager
    {
        private FileSystemStorage _storage;
        private KeyholeConnection _keyhole;
        private TileManager _tileManager;
        private MasterLayer _masterLayer;
        //private FlatEarthView _earthView; // 뷰는 뷰가
        private Action<MasterLayer> _loadMapCallback;
        private Dictionary<TileServerType, AbstractMap> _mapProvider = new Dictionary<TileServerType, AbstractMap>();

        private static MapManager _instance = null;
        public static MapManager Instance => _instance ?? (_instance = new MapManager());

        private MapManager()
        {
            Array allTileServerType = Enum.GetValues(typeof(TileServerType));

            foreach (TileServerType serverType in allTileServerType)
            {
                switch(serverType)
                {
                    case TileServerType.GoogleMaps:
                        _mapProvider.Add(serverType, new GoogleMap());
                        break;
                    case TileServerType.SkyVector_VFR:
                    case TileServerType.SkyVector_IFR_Low:
                    case TileServerType.SkyVector_IFR_High:
                        _mapProvider.Add(serverType, new SkyVectorMap(serverType));
                        break;
                    case TileServerType.OpenStreet:
                        _mapProvider.Add(serverType, new OpenStreetMap());
                        break;
                    default:
                        break;
                }
            }
        }

        public void RegisterLoadMapCallback(Action<MasterLayer> callback)
        {
            _loadMapCallback = callback;
        }

        public void LoadMap(TileServerType type)
        {
            if (_mapProvider.TryGetValue(type, out var map))
            {
                var filePath = map.GetFilePath();
                Directory.CreateDirectory(filePath);

                // Initialize filesystem storage
                _storage = new FileSystemStorage(filePath, useGe: true);

                // If using internet, chain keyhole connection
                if (map.IsInternet())
                {
                    _keyhole = new KeyholeConnection(map);
                    _keyhole.SetSaveStorage(_storage);
                    _storage.SetNextLoadStorage(_keyhole);
                }

                // TileManager and rendering layers
                _tileManager = new TileManager(_storage);
                _masterLayer = new GoogleLayer(_tileManager);
                //_earthView = new FlatEarthView(_masterLayer);

                _loadMapCallback?.Invoke(_masterLayer);
            }
        }

        internal void ClearTitleManager()
        {
            _tileManager.Cleanup();
        }
    }
}
