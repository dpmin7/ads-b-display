﻿// Required NuGet packages:
// - ScottPlot.WPF (v5.0.55)
// - Google.Cloud.BigQuery.V2

using Google.Apis.Auth.OAuth2;
using Google.Cloud.BigQuery.V2;
using MahApps.Metro.Controls;
using ScottPlot;
using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ADS_B_Display.Models.FlightAnalytics
{
    internal class FlightAnalytics
    {
        private readonly string projectId = "scs-lg-arch-5";
        private readonly string datasetId = "SBS_Data";
        private readonly string credentialFile = "YourJsonFile.json"; // Must be placed in "BigQuery" folder next to .exe

        public void AnalyzeFlightProfile(string hexIdent)
        {
            AnalyzeFlightProfileInternal(hexIdent, null);
        }

        public void AnalyzeFlightProfile(string hexIdent, string table)
        {
            AnalyzeFlightProfileInternal(hexIdent, table);
        }

        private void AnalyzeFlightProfileInternal(string hexIdent, string specificTable)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    var flightData = await GetInterpolatedFlightDataAsync(hexIdent, specificTable);

                    if (flightData.Count == 0)
                    {
                        MessageBox.Show($"No data for: {hexIdent}");
                        return;
                    }

                    var window = new MetroWindow
                    {
                        Width = 1000,
                        Height = 600,
                        Title = $"Flight Data - {hexIdent}",
                        Topmost = true
                    };

                    var source = new ResourceDictionary {
                        Source = new Uri("pack://application:,,,/MahApps.Metro;component/Styles/Themes/Dark.Steel.xaml",
                                    UriKind.Absolute)};
                    window.Resources.MergedDictionaries.Add(source);
                    var darkStyle = new ScottPlot.PlotStyles.Dark();

                    var plot = new WpfPlot();
                    plot.Margin = new Thickness(10);
                    plot.Plot.SetStyle(darkStyle);
                    window.Content = plot;

                    var times = flightData.Select(x => x.ts.ToOADate()).ToArray();
                    var speeds = flightData.Select(x => x.speed).ToArray();
                    var altitudes = flightData.Select(x => x.altitude).ToArray();

                    var plt = plot.Plot;

                    var speedPlot = plt.Add.Scatter(times, speeds);
                    speedPlot.LegendText = "Speed (knots)";

                    var rightAxis = plt.Axes.AddRightAxis();
                    var altPlot = plt.Add.Scatter(times, altitudes);
                    altPlot.LegendText = "Altitude (ft)";
                    altPlot.Axes.YAxis = rightAxis;
                    // 다크 테마를 한 번 더 덮어쓰기
                    plt.SetStyle(new ScottPlot.PlotStyles.Dark());
                    plot.Refresh();

                    plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic();
                    plt.Axes.Left.Label.Text = "Speed (knots)";
                    rightAxis.LabelText = "Altitude (ft)";

                    plt.Legend.IsVisible = true;
                    plt.Title($"Flight Info for {hexIdent}");

                    window.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            });
        }

        private async Task<List<(DateTime ts, double speed, double altitude)>> GetInterpolatedFlightDataAsync(string hexIdent, string specificTable = null)
        {
            var rawData = await FetchRawFlightData(hexIdent, specificTable);
            if (!rawData.Any()) return new List<(DateTime, double, double)>();

            var sorted = rawData.OrderBy(x => x.ts).ToList();
            var timestamps = sorted.Select(x => x.ts).ToList();
            var speeds = Interpolate(sorted.Select(x => x.speed).ToArray());
            var altitudes = Interpolate(sorted.Select(x => x.altitude).ToArray());

            var result = new List<(DateTime, double, double)>();
            for (int i = 0; i < timestamps.Count; i++)
                result.Add((timestamps[i], speeds[i], altitudes[i]));

            return result;
        }

        private async Task<List<(DateTime ts, double? speed, double? altitude)>> FetchRawFlightData(string hexIdent, string specificTable = null)
        {
            var exePath = Assembly.GetExecutingAssembly().Location;
            var homeDir = Path.GetDirectoryName(exePath);
            var credentialPath = Path.Combine(homeDir, "BigQuery", credentialFile);

            var credential = GoogleCredential.FromFile(credentialPath);
            var client = BigQueryClient.Create(projectId, credential);
            var results = new List<(DateTime, double?, double?)>();

            if (!string.IsNullOrEmpty(specificTable))
            {
                string sql = $@"
                    SELECT Timestamp, GroundSpeed, Altitude
                    FROM `{projectId}.{datasetId}.{specificTable}`
                    WHERE HexIdent = '{hexIdent}'
                    ORDER BY Timestamp";

                var query = await client.ExecuteQueryAsync(sql, parameters: null);
                foreach (var row in query)
                {
                    long ts = (long)row["Timestamp"];
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime;
                    double? speed = row["GroundSpeed"]?.ToDouble();
                    double? alt = row["Altitude"]?.ToDouble();
                    results.Add((dt, speed, alt));
                }
            }
            else
            {
                var dataset = client.GetDataset(datasetId);
                var tables = dataset.ListTables();

                foreach (var table in tables)
                {
                    string sql = $@"
                        SELECT Timestamp, GroundSpeed, Altitude
                        FROM `{projectId}.{datasetId}.{table.Reference.TableId}`
                        WHERE HexIdent = '{hexIdent}'
                        ORDER BY Timestamp";

                    try
                    {
                        var query = await client.ExecuteQueryAsync(sql, parameters: null);
                        foreach (var row in query)
                        {
                            long ts = (long)row["Timestamp"];
                            var dt = DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime;
                            double? speed = row["GroundSpeed"]?.ToDouble();
                            double? alt = row["Altitude"]?.ToDouble();
                            results.Add((dt, speed, alt));
                        }
                    }
                    catch { }
                }
            }

            return results;
        }

        private double[] Interpolate(double?[] input)
        {
            double[] result = new double[input.Length];
            int? lastValid = null;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i].HasValue)
                {
                    result[i] = input[i].Value;
                    lastValid = i;
                }
                else
                {
                    int? nextValid = null;
                    for (int j = i + 1; j < input.Length; j++)
                    {
                        if (input[j].HasValue)
                        {
                            nextValid = j;
                            break;
                        }
                    }

                    if (lastValid.HasValue && nextValid.HasValue)
                    {
                        double v1 = input[lastValid.Value].Value;
                        double v2 = input[nextValid.Value].Value;
                        double t = (double)(i - lastValid.Value) / (nextValid.Value - lastValid.Value);
                        result[i] = v1 + (v2 - v1) * t;
                    }
                    else if (lastValid.HasValue)
                    {
                        result[i] = input[lastValid.Value].Value;
                    }
                    else if (nextValid.HasValue)
                    {
                        result[i] = input[nextValid.Value].Value;
                    }
                    else
                    {
                        result[i] = 0;
                    }
                }
            }

            return result;
        }
    }

    internal static class RowExtensions
    {
        public static double? ToDouble(this object val)
        {
            if (val == null || val is DBNull) return null;
            return Convert.ToDouble(val);
        }
    }
}
