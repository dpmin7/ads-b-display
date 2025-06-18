using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    /// <summary>
    /// Defines available tile server types.
    /// </summary>
    public enum TileServerType
    {
        GoogleMaps = 0,
        SkyVector_VFR = 1,
        SkyVector_IFR_Low = 2,
        SkyVector_IFR_High = 3
    }

    /// <summary>
    /// Connection to Google and SkyVector servers for tile fetching.
    /// Converted from KeyholeConnection.h/.cpp
    /// </summary>
    public class KeyholeConnection : SimpleTileStorage
    {
        private readonly HttpClient _httpClient;
        private readonly TileServerType _serverType;
        private readonly string _key;
        private readonly string _chart;
        private readonly string _edition;

        private const string GoogleUrl = "http://mt1.google.com";
        private const string SkyVectorUrl = "http://t.skyvector.com";
        private const string SkyVectorKey = "V7pMh4xRihf1nr61";
        private const string SkyVectorEdition = "2504";

        public KeyholeConnection(TileServerType serverType) : base()
        {
            _httpClient = new HttpClient();
            _serverType = serverType;

            // SkyVector 모드에서만 key/chart/edition 설정
            if (serverType != TileServerType.GoogleMaps) {
                _key = SkyVectorKey;
                _edition = SkyVectorEdition;
                switch (serverType) {
                    case TileServerType.SkyVector_VFR:
                        _chart = "301";
                        break;
                    case TileServerType.SkyVector_IFR_Low:
                        _chart = "302";
                        break;
                    case TileServerType.SkyVector_IFR_High:
                        _chart = "304";
                        break;
                    default:
                        throw new ArgumentException("Unknown SkyVector mode");
                }
            }
            // GoogleMaps 모드는 아무것도 설정하지 않음
        }

        /// <summary>
        /// Download and process a tile.
        /// </summary>
        protected override void Process(Tile tile)
        {
            string url;
            if (_serverType == TileServerType.GoogleMaps)
                url = $"{GoogleUrl}/vt/lyrs=m&x={tile.X}&y={tile.Y}&z={tile.Level}";
            else
                url = $"{SkyVectorUrl}/tiles.aspx?x={tile.X}&y={tile.Y}&z={tile.Level}&k={_key}&c={_chart}&e={_edition}";

            var response = _httpClient.GetAsync(url).Result;
            if (!response.IsSuccessStatusCode) {
                tile.Null();
                return;
            }

            var data = response.Content.ReadAsByteArrayAsync().Result;
            tile.Load(data, SaveStorage != null);
        }
    }
}
