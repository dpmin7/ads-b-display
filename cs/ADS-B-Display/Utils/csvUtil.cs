using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Utils
{
    public static class CsvUtil
    {
        public static List<Dictionary<string, string>> Parse(string data)
        {
            var result = new List<Dictionary<string, string>>();

            var lines = data.Split('\n');
            if (lines.Length == 0) return result;

            var headers = lines[0].Split(',');

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var values = line.Split(',');
                var row = new Dictionary<string, string>();

                for (int j = 0; j < headers.Length; j++)
                {
                    string key = headers[j].Trim().Trim('"');
                    string value = j < values.Length ? values[j].Trim().Trim('"') : "";

                    row[key] = value;
                }

                result.Add(row);
            }

            return result;
        }
    }
}
