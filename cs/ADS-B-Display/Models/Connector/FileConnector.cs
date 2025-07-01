using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.Connector
{
    public class FileConnector: IConnector
    {
        private readonly string _filePath;
        private int _playbackSpeed = 1;
        private Thread _thread;

        public FileConnector(string filePath)
        {
            _filePath = filePath;
        }

        public void Start(Func<string, long, uint> onMessageReceived, int playBackSpeed, InputSourceState state)
        {
            _playbackSpeed = playBackSpeed;

            _thread = new Thread(() =>
            {
                try
                {
                    using (var reader = new StreamReader(_filePath))
                    {
                        while (state.Running)
                        {
                            string rawLine = reader.ReadLine();
                            if (string.IsNullOrEmpty(rawLine))
                                continue;
                            var time = long.Parse(rawLine);
                            if (state.First)
                            {
                                state.First = false;
                                state.LastTime = time;
                            }
                            var sleepTime = (int)(time - state.LastTime);
                            state.LastTime = time;
                            Thread.Sleep(sleepTime / _playbackSpeed);

                            rawLine = reader.ReadLine();
                            if (string.IsNullOrEmpty(rawLine))
                                continue;

                            onMessageReceived?.Invoke(rawLine, time);
                        }
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

        public void SetPlaybackSpeed(int speed)
        {
            _playbackSpeed = speed;
        }

        public void Stop()
        {
            _thread?.Join();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}