using ADS_B_Display.Models;
using NLog;
using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

// 예시: PingEcho 인스턴스 생성 및 시작
// var pingEcho = new PingEcho();
// pingEcho.Start("8.8.8.8", 2000, (host, ex) =>
// {
//     if (ex != null)
//         Console.WriteLine($"Ping 예외 발생: {host} - {ex.Message}");
//     else
//         Console.WriteLine($"Ping 실패: {host}");
// });

//// 중지할 때
//// pingEcho.Stop();
///

namespace ADS_B_Display.Models
{
    internal class PingEcho : IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private Timer _timer;
        private string _host;
        private int _port;
        private int _rcvIntervalMs;
        private Action<string, int, bool> _onPingNotifier;
        private bool _isRunning;
        private readonly object _lock = new object();
        private bool _isConnected;
        private const int _defaultIntervalMs = 2000; // 기본 Ping 주기 (ms)

        /// <summary>
        /// PingEcho를 시작합니다.
        /// </summary>
        /// <param name="host">Ping 대상 호스트(IP 또는 도메인)</param>
        /// <param name="intervalMs">Ping 주기(ms)</param>
        /// <param name="onPingFailed">Ping 실패 시 호출될 콜백 (host, 예외)</param>
        public void Start(string host, int port, int intervalMs, Action<string, int, bool> onPingNotifier)
        {
            lock (_lock)
            {
                if (_isRunning)
                    return;

                _host = host;
                _port = port; 
                _rcvIntervalMs = intervalMs;
                _onPingNotifier = onPingNotifier;
                _timer = new Timer(OnTimer, null, 0, intervalMs);
                _isRunning = true;
                _isConnected = true;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
                _isRunning = false;
            }
        }

        private bool IsPortOpen(string host, int port, int timeoutMs = 1000)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(timeoutMs);
                    if (!success)
                        return false;

                    client.EndConnect(result);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void OnTimer(object state)
        {
            try
            {
                // TCP의 특정 포트를 확인하는 동작
                var bReply = IsPortOpen(_host, _port, 1000);
                if (_isConnected != bReply)
                {
                    _isConnected = bReply;
                    _onPingNotifier?.Invoke(_host, _port, bReply);

                    if (bReply == true)
                    {
                        _timer?.Change(_rcvIntervalMs, _rcvIntervalMs); // 연결 성공 시 주기 재설정
                    }
                    else
                    {
                        _timer?.Change(_defaultIntervalMs, _defaultIntervalMs); // 연결 성공 시 주기 재설정
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"PingEcho 예외 발생: {_host}");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}