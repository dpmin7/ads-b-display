using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.CPA
{
    internal class CollisionRiskWorker
    {
        private static DateTime _lastCpaRun = DateTime.MinValue;
        private static readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
        private static bool _isRunning = false;

        public static void RunPeriodicCPA()
        {
            if (_isRunning) return;

            if (DateTime.Now - _lastCpaRun >= _interval)
            {
                _isRunning = true;
                Task.Run(() =>
                {
                    try
                    {
                        CPAProcessor.CalculateAllCPA();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CPA] Error: {ex.Message}");
                    }
                    finally
                    {
                        _lastCpaRun = DateTime.Now;
                        _isRunning = false;
                    }
                });
            }
        }
    }
}
