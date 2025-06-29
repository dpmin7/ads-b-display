using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.CPA
{
    internal class CollisionRiskWorker
    {
        private static bool _isRunning = false;
        private static CancellationTokenSource _cts;
        

        public static void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            var processor = new CPAProcessor();

            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        processor.CalculateAllCPA();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CPA] Error: {ex.Message}");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
                }
            }, _cts.Token);
        }

        public static void Stop()
        {
            if (!_isRunning) return;

            _cts.Cancel();
            _isRunning = false;
        }
    }
}
