using ADS_B_Display_NET.Map.MapSrc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ADS_B_Display
{
    public static class AirportDB
    {
        private static List<Dictionary<string, string>> airports;
        private static List<Dictionary<string, string>> routes;
        private static HashSet<string> uniqueAirportCodes = new HashSet<string>();

        private const string airportsUrl = "https://vrs-standing-data.adsb.lol/airports.csv";
        private const string routesUrl = "https://vrs-standing-data.adsb.lol/routes.csv";

        static AirportDB()
        {
            Task.Run(() =>
            {
                HttpClient _httpClient = new HttpClient();

                var airportsResponse = _httpClient.GetAsync(airportsUrl).Result;
                if (!airportsResponse.IsSuccessStatusCode)
                {
                    return;
                }

                var airportsData = airportsResponse.Content.ReadAsStringAsync().Result;
                airports = Parse(airportsData);

                var routesResponse = _httpClient.GetAsync(routesUrl).Result;
                if (!routesResponse.IsSuccessStatusCode)
                {
                    return;
                }

                var routesData = routesResponse.Content.ReadAsStringAsync().Result;
                routes = Parse(routesData);
                SetUniqueAirportCodes();
            });
        }

        private static List<Dictionary<string, string>> Parse(string data)
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
                    string key = headers[j].Trim();
                    string value = j < values.Length ? values[j].Trim() : "";

                    row[key] = value;
                }

                result.Add(row);
            }

            return result;
        }

        public static List<Dictionary<string, string>> GetAirPortsInfo()
        {
            return airports;
        }

        public static List<Dictionary<string, string>> GetRoutesInfo()
        {
            return routes;
        }

        private static void SetUniqueAirportCodes()
        {
           uniqueAirportCodes = new HashSet<string>();

            foreach (var row in routes)
            {
                string airportCodes = row["AirportCodes"];
                String[] airportCode = airportCodes.Split('-');
                foreach (string airport in airportCode)
                {
                    uniqueAirportCodes.Add(airport);
                }

            }
        }

        public static HashSet<string> GetUniqueAirportCodes()
        {
            return uniqueAirportCodes;
        }
    }
}
