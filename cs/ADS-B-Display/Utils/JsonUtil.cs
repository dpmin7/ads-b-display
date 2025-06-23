using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ADS_B_Display.Utils
{
    internal class JsonUtil
    {
        public static string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            });
        }
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            });
        }
        public static bool TryDeserialize<T>(string json, out T result)
        {
            try
            {
                result = Deserialize<T>(json);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }
        public static bool TryDeserializeFromFile<T>(string path, out T result)
        {
            result = default;
            if (File.Exists(path)) {
                var str = File.ReadAllText(path);
                return TryDeserialize(str, out result);
            }

            return false;
        }
    }
}
