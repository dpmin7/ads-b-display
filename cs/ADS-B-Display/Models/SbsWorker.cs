using Google.Apis.Bigquery.v2.Data;
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
        //private BigQuery bigQuery;
        private IDbWriterReader _dbWriterReader;

        public SbsWorker(Func<string, long, uint> onMessageReceived)
        {
            OnMessageReceived = onMessageReceived;
        }

        public bool Start(string path, bool useDb = false, IDbWriterReader dbWriterReader = null)
        {
            try {
                _first = true;
                _running = true;
                _filePath = path;
                _useDb = useDb;
                _dbWriterReader = dbWriterReader;
                _thread = new Thread(Run) { IsBackground = true };
                _thread.Start();

                return true;
            } catch (Exception ex) {
                return false;
            }
        }

        public async Task<bool> Start(string host, int port, CancellationToken ct)
        {
            _running = true;
            _tcpClient = new TcpClient();
            using (ct.Register(() => {
                try { _tcpClient.Close(); } catch { }
            })) {
                try {
                    await _tcpClient.ConnectAsync(host, port);
                    _thread = new Thread(Run) { IsBackground = true };
                    _thread.Start();
                    return true;
                } catch (OperationCanceledException) {
                    throw new TimeoutException("TCP 연결이 취소되었거나 타임아웃되었습니다.");
                } catch (TimeoutException) {
                    MessageBox.Show("Connection timeout. Please check your network.");
                    _running = false;
                    return false;
                }
                catch (Exception) {
                    _tcpClient.Dispose();
                    _running = false;
                    return false;
                }
            }
        }

        public void RecordOn(string path, bool _useDb = false, IDbWriterReader dbWriterReader = null)
        {
            if (_useDb)
            {
                if (dbWriterReader == null)
                {
                    Console.WriteLine("DB Writer/Reader is not initialized.");
                    return;
                }

                _dbWriterReader = dbWriterReader;
                _dbWriterReader.SetPathCsvFileName();
                _dbWriterReader.CreateCsvWriter();
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
                    _dbWriterReader.Close();
                    _dbWriterReader = null;

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
            _dbWriterReader.SetPathCsvFileName();
            _dbWriterReader.DeleteAllCsvFiles();

            // BigQuery에서 데이터를 비동기로 큐에 쌓기 시작
            _dbWriterReader.ReadDataFromDatabase();

            if (_dbWriterReader == null)
                return;

            try
            {
                while (_running)
                {
                    if (_dbWriterReader == null)
                        break;

                    string rawLine = _dbWriterReader.ReadRow();

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

        //        private void RunDatabaseMode()
        //        {
        //            _dbWriterReader.SetPathCsvFileName();
        //#if true
        //            _dbWriterReader.DeleteAllCsvFiles();

        //            // Database 데이터 읽기 (Query 해서 CSV 파일 읽기)
        //            _dbWriterReader.ReadDataFromDatabase();
        //#endif
        //            if (_dbWriterReader == null)
        //            {
        //                return;
        //            }

        //            // Database CSV 리더 생성
        //            _dbWriterReader.CreateCsvReader();

        //            try
        //            {
        //                while (_running)
        //                {
        //                    if (_dbWriterReader == null)
        //                    {
        //                        break;
        //                    }

        //                    string rawLine = _dbWriterReader.ReadRow();

        //                    // 파일이 더 이상 없거나, 읽을 데이터가 없으면 종료
        //                    if (rawLine == null)
        //                    {
        //                        MessageBox.Show("Database Playback End");
        //                        break;
        //                    }

        //                    // 빈 줄 또는 헤더는 건너뜀
        //                    if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith("Timestamp,"))
        //                        continue;

        //                    // 첫 번째 열(timestamp)와 나머지(row) 분리
        //                    var parts = rawLine.Split(new[] { ',' }, 2);
        //                    if (parts.Length < 2)
        //                        continue;

        //                    if (!long.TryParse(parts[0], out var time))
        //                        continue;

        //                    if (_first)
        //                    {
        //                        _first = false;
        //                        _lastTime = time;
        //                    }
        //                    var _sleepTime = (int)(time - _lastTime);
        //                    _lastTime = time;
        //                    if (_sleepTime > 0)
        //                        Thread.Sleep(_sleepTime / _playBackSpeed);

        //                    string row = parts[1];
        //                    if (string.IsNullOrEmpty(row))
        //                        continue;

        //                    OnMessageReceived?.Invoke(row, time);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                MessageBox.Show("Database Playback Error: " + ex.Message);
        //            }

        //            OnFinished?.Invoke();
        //        }

        private void RunFileMode()
        {
            try {
                using (var reader = new StreamReader(_filePath)) {
                    while (_running) {
                        string rawLine = reader.ReadLine();
                        if (string.IsNullOrEmpty(rawLine))
                            continue;
                        var time = long.Parse(rawLine);
                        if (_first) {
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
            } catch (Exception ex) {
                MessageBox.Show("File Playback Error: " + ex.Message);
            }

            OnFinished?.Invoke();
        }

        private void RunTcpMode()
        {
            try {
                using (var reader = new StreamReader(_tcpClient.GetStream())) {
                    while (_running && _tcpClient.Connected) {
                        string msg = reader.ReadLine();
                        if (msg == null) continue;
                        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        ProcessMessage(msg, time);
                    }
                }
            } catch (Exception ex) {
                //MessageBox.Show("TCP Error: " + ex.Message); //리 컨넥션 필수
            }

            OnFinished?.Invoke();
        }

        private void ProcessMessage(string msg, long timestamp)
        {
            RecordStream?.WriteLine(timestamp);
            RecordStream?.WriteLine(msg);

            _dbWriterReader?.WriteRow(timestamp, msg);

            uint acio = OnMessageReceived?.Invoke(msg, timestamp) ?? 0;
            PostProcess(acio);
        }

        private void PostProcess(uint acio)
        {
            if (AircraftManager.TryGet(acio, out var aircraft))
            {
                aircraft.TrackPoint.Enqueue(new AircraftTrackPoint(
                    aircraft.Latitude, aircraft.Longitude, aircraft.Altitude, aircraft.LastSeen));
            }
        }
    }
}
