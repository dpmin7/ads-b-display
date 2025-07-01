using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ADS_B_Display.Models;

namespace ADS_B_Display
{
    public class SbsWorker
    {
        enum MsgType { SbsMsg, RawMsg }
        private string _filePath;
        private TcpClient _tcpClient;
        private Thread _thread;
        private volatile bool _running;
        private bool _first = true;
        private long _lastTime;
        private int _playBackSpeed = 1;

        private readonly Func<string, long, uint> OnMessageReceived;

        private Action OnFinished;

        public StreamWriter RecordStream;

        private bool _useDb = false;

        private IDBConnector _dbConnector;

        public SbsWorker(Func<string, long, uint> onMessageReceived)
        {
            OnMessageReceived = onMessageReceived;
        }

        public bool Start(string path, bool useDb = false, IDBConnector dbConnector = null)
        {
            try
            {
                _first = true;
                _running = true;
                _filePath = path;
                _useDb = useDb;
                _dbConnector = dbConnector;
                _thread = new Thread(Run) { IsBackground = true };
                _thread.Start();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> Start(string host, int port, CancellationToken ct)
        {
            _running = true;
            _tcpClient = new TcpClient();
            using (ct.Register(() =>
            {
                try { _tcpClient.Close(); } catch { }
            }))
            {
                try
                {
                    await _tcpClient.ConnectAsync(host, port);
                    _thread = new Thread(Run) { IsBackground = true };
                    _thread.Start();
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("TCP 연결이 취소되었거나 타임아웃되었습니다.");
                }
                catch (TimeoutException)
                {
                    MessageBox.Show("Connection timeout. Please check your network.");
                    _running = false;
                    return false;
                }
                catch (Exception)
                {
                    _tcpClient.Dispose();
                    _running = false;
                    return false;
                }
            }
        }

        public void RecordOn(string path, bool _useDb = false, IDBConnector dbConnector = null)
        {
            if (_useDb)
            {
                if (dbConnector == null)
                {
                    Console.WriteLine("DB Connector is not initialized.");
                    return;
                }

                _dbConnector = dbConnector;
            }
            else // File Mode
            {
                RecordStream = new StreamWriter(path, append: true)
                {
                    AutoFlush = true
                };
            }
        }

        public void RecordOff(bool useDb = false)
        {
            if (_useDb)
            {
                try
                {
                    _dbConnector.Close();
                    _dbConnector = null;

                    MessageBox.Show("DB End");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"DB 기록 파일을 닫는 동안 오류가 발생했습니다:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else // File mode
            {
                try
                {
                    RecordStream?.Flush();
                    RecordStream?.Close();
                    RecordStream = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"SBS 기록 파일을 닫는 동안 오류가 발생했습니다:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void Stop(bool useDb = false)
        {
            RecordOff(useDb); // 레코딩 중에 멈추면 레코딩 종료부터 하자.
            _running = false;
            if (_tcpClient != null)
            {
                _tcpClient.Close();
            }

            _thread?.Join();
        }
        public void setPlayBackSpeed(int speed)
        {
            _playBackSpeed = speed;
        }

        private void Run()
        {
            AircraftManager.PurgeAll();

            if (_useDb == true)
            {
                RunDatabaseMode();
                return;
            }

            if (_filePath != null)
            {
                RunFileMode();
                return;
            }

            if (_tcpClient != null)
            {
                RunTcpMode();
                return;
            }
        }

        private void RunDatabaseMode()
        {
            // Database에서 데이터를 비동기로 큐에 쌓기 시작
            _dbConnector.ReadDataFromDatabase();

            if (_dbConnector == null)
                return;

            try
            {
                while (_running)
                {
                    if (_dbConnector == null)
                        break;

                    string rawLine = _dbConnector.ReadRow();

                    // 데이터가 모두 끝나면 null 반환
                    if (rawLine == null)
                    {
                        MessageBox.Show("Database Playback End");
                        break;
                    }

                    // 빈 줄 또는 헤더는 건너뜀
                    if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith("Timestamp,"))
                        continue;

                    var parts = rawLine.Split(new[] { ',' }, 2);
                    if (parts.Length < 2)
                        continue;

                    if (!long.TryParse(parts[0], out var time))
                        continue;

                    if (_first)
                    {
                        _first = false;
                        _lastTime = time;
                    }
                    var _sleepTime = (int)(time - _lastTime);
                    _lastTime = time;
                    if (_sleepTime > 0)
                        Thread.Sleep(_sleepTime / _playBackSpeed);

                    string row = parts[1];
                    if (string.IsNullOrEmpty(row))
                        continue;

                    OnMessageReceived?.Invoke(row, time);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database Playback Error: " + ex.Message);
            }

            OnFinished?.Invoke();
        }

        private void RunFileMode()
        {
            try
            {
                using (var reader = new StreamReader(_filePath))
                {
                    while (_running)
                    {
                        string rawLine = reader.ReadLine();
                        if (string.IsNullOrEmpty(rawLine))
                            continue;
                        var time = long.Parse(rawLine);
                        if (_first)
                        {
                            _first = false;
                            _lastTime = time;
                        }
                        var _sleepTime = (int)(time - _lastTime);
                        _lastTime = time;
                        Thread.Sleep(_sleepTime / _playBackSpeed);

                        rawLine = reader.ReadLine();
                        if (string.IsNullOrEmpty(rawLine))
                            continue;

                        uint acio = OnMessageReceived?.Invoke(rawLine, time) ?? 0;
                        PostProcess(acio);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("File Playback Error: " + ex.Message);
            }

            OnFinished?.Invoke();
        }

        private void RunTcpMode()
        {
            try
            {
                using (var reader = new StreamReader(_tcpClient.GetStream()))
                {
                    while (_running && _tcpClient.Connected)
                    {
                        string msg = reader.ReadLine();
                        if (msg == null) continue;
                        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        ProcessMessage(msg, time);
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show("TCP Error: " + ex.Message); //리 컨넥션 필수
            }

            OnFinished?.Invoke();
        }

        private void ProcessMessage(string msg, long timestamp)
        {
            RecordStream?.WriteLine(timestamp);
            RecordStream?.WriteLine(msg);

            _dbConnector?.WriteRow(timestamp, msg);

            uint acio = OnMessageReceived?.Invoke(msg, timestamp) ?? 0;
            PostProcess(acio);
        }

        private void PostProcess(uint acio)
        {
            if (AircraftManager.TryGet(acio, out var aircraft))
            {
                if (aircraft.HaveLatLon)
                {
                    if (aircraft.TrackPoint.Items.Count >= 1)
                    {
                        var trackInfo = aircraft.TrackPoint.Items.ElementAt(0);

                        if (trackInfo.Latitude == aircraft.Latitude && trackInfo.Longitude == aircraft.Longitude)
                        {
                            aircraft.Speed = 0;
                        }

                    }
                    if (aircraft.HaveLatLon)
                    {
                        aircraft.TrackPoint.Enqueue(new AircraftTrackPoint(
                        aircraft.Latitude, aircraft.Longitude, aircraft.Altitude, aircraft.LastSeen));
                    }
                }


            }

        }
    }


public static class AircraftUtils
    {
        // 지구 반지름 (km)
        private const double EarthRadiusKm = 6371.0;

        // 두 위경도 사이 거리 계산 (Haversine 공식)
        public static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);
            double radLat1 = DegreesToRadians(lat1);
            double radLat2 = DegreesToRadians(lat2);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(radLat1) * Math.Cos(radLat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c;
        }

        private static double DegreesToRadians(double deg) => deg * (Math.PI / 180.0);
    }
}
