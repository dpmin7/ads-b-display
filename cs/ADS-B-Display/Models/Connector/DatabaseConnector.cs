using System;
using System.Threading;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.Connector
{
    public class DatabaseConnector: IConnector
    {
        private readonly IDBConnector _dbConnector;
        private Thread _thread;

        public DatabaseConnector(IDBConnector dbConnector)
        {
            _dbConnector = dbConnector;
        }

        public void Start(Func<string, long, uint> onMessageReceived, int playBackSpeed, InputSourceState state)
        {
            _thread = new Thread(() =>
            {
                _dbConnector.ReadDataFromDatabase();
                try
                {
                    while (state.Running)
                    {
                        string rawLine = _dbConnector.ReadRow();
                        if (rawLine == null)
                            break;
                        if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith("Timestamp,"))
                            continue;
                        var parts = rawLine.Split(new[] { ',' }, 2);
                        if (parts.Length < 2)
                            continue;
                        if (!long.TryParse(parts[0], out var time))
                            continue;
                        if (state.First)
                        {
                            state.First = false;
                            state.LastTime = time;
                        }
                        var sleepTime = (int)(time - state.LastTime);
                        state.LastTime = time;
                        if (sleepTime > 0)
                            Thread.Sleep(sleepTime / playBackSpeed);

                        string row = parts[1];
                        if (string.IsNullOrEmpty(row))
                            continue;

                        onMessageReceived?.Invoke(row, time);
                    }
                }
                catch { }
            })
            { IsBackground = true };
            _thread.Start();
        }

        public Task StartAsync(Func<string, long, uint> onMessageReceived, int playBackSpeed, CancellationToken ct, InputSourceState state)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            _dbConnector?.Close();
            _thread?.Join();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}