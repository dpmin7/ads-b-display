using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.Connector
{
    public class TcpConnector : IConnector
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient _tcpClient;
        private Thread _thread;

        public TcpConnector(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Start(Func<string, long, uint> onMessageReceived, int playBackSpeed, InputSourceState state)
        {
            throw new NotImplementedException();
        }

        public async Task StartAsync(Func<string, long, uint> onMessageReceived, int playBackSpeed, CancellationToken ct, InputSourceState state)
        {
            _tcpClient = new TcpClient();
            using (ct.Register(() => { try { _tcpClient.Close(); } catch { } }))
            {
                await _tcpClient.ConnectAsync(_host, _port);
                _thread = new Thread(() =>
                {
                    try
                    {
                        using (var reader = new StreamReader(_tcpClient.GetStream()))
                        {
                            while (state.Running && _tcpClient.Connected)
                            {
                                string msg = reader.ReadLine();
                                if (msg == null) continue;
                                long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                onMessageReceived?.Invoke(msg, time);
                            }
                        }
                    }
                    catch { }
                })
                { IsBackground = true };
                _thread.Start();
            }
        }

        public void Stop()
        {
            _tcpClient?.Close();
            _thread?.Join();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}