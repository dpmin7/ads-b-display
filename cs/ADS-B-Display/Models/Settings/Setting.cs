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
        public ControlSettings ControlSettings { get; set; } = new ControlSettings();
        public MapViewConfig MapConfig { get; set; } = new MapViewConfig();

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

    internal class MapViewConfig
    {
        public double EyeX { get; set; }
        public double EyeY { get; set; }
        public double EyeH { get; set; }
        public bool IsInitialState => EyeX == 0 && EyeY == 0 && EyeH == 0;
    }

    public class ControlSettings
    {
        public TileServerType ServerType { get; set; } = TileServerType.GoogleMaps;

        // Display Map
        public bool DisplayMapEnabled { get; set; } = true;
        public int PurgeDuration { get; set; } = 90;
        public bool PurgeStale { get; set; } = false;
        public bool CycleImages { get; set; } = false;

        // Raw Connection
        public string RawAddress { get; set; } = "127.0.0.1";
        public bool RawConnectOnStartup { get; set; }

        // SBS Connection
        public string SbsAddress { get; set; } = "128.237.96.41";
        public bool SbsConnectOnStartup { get; set; }


        // Areas of Interest
        public List<AreaList> AreaList { get; set; } = new List<AreaList>();

        // ETC
        public string MapProvider { get; set; } = "GoogleMaps";
        public bool UseTimeToGo { get; set; } = true;
        public double TimeToGoValue { get; set; } = 300; // 5 minutes in seconds

        public bool UseBigQuery { get; set; } = false;
    }

    public class AreaList
    {
        public string Name { get; set; }
        public List<AreaOfInterest> AreaOfInterestList { get; set; } = new List<AreaOfInterest>();
    }

    public class AreaOfInterest
    {
        public string Name { get; set; }
        public List<Pos> Area { get; set; } = new List<Pos>();
        public string Color { get; set; } // 또는 System.Windows.Media.Color 등
    }

    public struct Pos
    {
        public double X { get; set; }
        public double Y{ get; set; }
    }
}
