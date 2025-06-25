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

    /// <summary>
    /// Connection to Google and SkyVector servers for tile fetching.
    /// Converted from KeyholeConnection.h/.cpp
    /// </summary>
    public class KeyholeConnection : SimpleTileStorage
    {
        private readonly HttpClient _httpClient;
        private readonly TileServerType _serverType;
        private readonly AbstractMap map;

        public KeyholeConnection(AbstractMap map) : base()
        {
            this.map = map;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }

        /// <summary>
        /// Download and process a tile.
        /// </summary>
        protected override async Task Process(Tile tile)
        {
            var response = await _httpClient.GetAsync(map.GetUrl(tile.X, tile.Y, tile.Level));
            if (!response.IsSuccessStatusCode) {
                tile.Null();
                return;
            }

            var data = await response.Content.ReadAsByteArrayAsync();

            try
            {
                tile.Load(data, SaveStorage != null);
                if (!tile.IsLoaded)
                    Console.WriteLine($"타일 로드 실패: z={tile.Level}, x={tile.X}, y={tile.Y}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"예외 발생: {ex.Message}");
            }
        }
    }
}
