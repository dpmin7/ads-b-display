using ADS_B_Display.Map.MapSrc;
using ADS_B_Display.Utils;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.Settings
{
    internal class Setting
    {
        public const string Dir = "Settings";
        public const string Name = "Setting.json";
        private static Setting _instance = null;
        public static Setting Instance => _instance ?? (_instance = new Setting());
        public TcpConfig TcpConfig { get; set; } = new TcpConfig();
        public MapConfig MapConfig { get; set; } = new MapConfig();

        public static bool Load()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.Combine(currentDir, Dir);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            var filePath = Path.Combine(path, Name);
            if (File.Exists(filePath) && JsonUtil.TryDeserializeFromFile(filePath, out _instance)) {
                return true;
            } else {
                string jsonStr = JsonUtil.Serialize(Instance);
                File.WriteAllText(filePath, jsonStr, Encoding.UTF8);
                return false;
            }
        }

        public static void Save()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.Combine(currentDir, Dir);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            var filePath = Path.Combine(path, Name);
            string jsonStr = JsonUtil.Serialize(_instance);
            File.WriteAllText(filePath, jsonStr, Encoding.UTF8);
        }
    }

    internal class TcpConfig
    {
        public string Address { get; set; }
        public int Port { get; set; }
    }

    internal class MapConfig
    {
        public bool IsDisplayMap { get; set; } = true;
        public TileServerType ServerType { get; set; } = TileServerType.GoogleMaps;
        public double EyeX { get; set; }      // longitude [-0.5..0.5]
        public double EyeY { get; set; }      // latitude  [-0.25..0.25]
        public double EyeH { get; set; }      // height above surface
    }
}
