using ADS_B_Display.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

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
                airports = CsvUtil.Parse(airportsData);

                var routesResponse = _httpClient.GetAsync(routesUrl).Result;
                if (!routesResponse.IsSuccessStatusCode)
                {
                    return;
                }

                var routesData = routesResponse.Content.ReadAsStringAsync().Result;
                routes = CsvUtil.Parse(routesData);
                SetUniqueAirportCodes();
            });
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
