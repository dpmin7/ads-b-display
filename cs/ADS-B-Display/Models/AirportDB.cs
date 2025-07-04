﻿using ADS_B_Display.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

namespace ADS_B_Display
{
    public static class AirportDB
    {
        private static List<Dictionary<string, string>> airports;
        private static List<Dictionary<string, string>> routes;
        private static HashSet<string> uniqueAirportCodes = new HashSet<string>();

        static AirportDB()
        {
            Task.Run(() =>
            {
                string airportsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Airport", "airports.csv");
                string routesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Airport", "routes.csv");

                if (!File.Exists(airportsPath))
                    return;

                string airportData = File.ReadAllText(airportsPath);

                airports = CsvUtil.Parse(airportData);

                if (!File.Exists(routesPath))
                    return;

                string routesData = File.ReadAllText(routesPath);

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
