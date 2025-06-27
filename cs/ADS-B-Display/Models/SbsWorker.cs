using ADS_B_Display.Models;
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

        private readonly Func<string, uint> OnMessageReceived;

        private Action OnFinished;

        public StreamWriter RecordStream;

        private bool _useBigQuery = false;
        private BigQuery bigQuery;

        public SbsWorker(Func<string, uint> onMessageReceived)
        {
            OnMessageReceived = onMessageReceived;
        }

        public bool Start(string path, bool useBigQuery = false)
        {
            try {
                _first = true;
                _running = true;
                _filePath = path;
                _useBigQuery = useBigQuery;
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

        public void RecordOn(string path, bool useBigQuery = false)
        {
            if (useBigQuery) // BigQuery Mode
            {
                bigQuery = new BigQuery("", useBigQuery);
                bigQuery.SetPathBigQueryCsvFileName();
                bigQuery.CreateCsvWriter();
                //bigQuery.DeleteBigQueryData();
            }
            else // File Mode
            {
                RecordStream = new StreamWriter(path, append: true)
                {
                    AutoFlush = true
                };
            }
        }

        public void RecordOff(bool useBigQuery = false)
        {
            if (useBigQuery) // BigQuery Mode
            {
                try
                {
                    bigQuery.Close();
                    bigQuery = null;

                    MessageBox.Show("BigQuery End");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"BigQuery 기록 파일을 닫는 동안 오류가 발생했습니다:\n{ex.Message}",
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

        public void Stop(bool useBigQuery = false)
        {   
            RecordOff(useBigQuery); // 레코딩 중에 멈추면 레코딩 종료부터 하자.
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

            if (_useBigQuery == true)
            {
                RunBigQueryMode();
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

        private void RunBigQueryMode()
        {
            bigQuery = new BigQuery(_filePath, true);

            bigQuery.SetPathBigQueryCsvFileName();

#if true
            bigQuery.DeleteAllCsvFiles();

            // BigQuery 데이터 읽기 (Query에서 CSV 파일 읽기)
            bigQuery.ReadBigQueryData();
#endif
            if (bigQuery == null)
            {
                return;
            }

            // BigQuery CSV 리더 생성
            bigQuery.CreateCsvReader();

            try
            {
                while (_running)
                {
                    if (bigQuery == null)
                    {
                        break;
                    }

                    string rawLine = bigQuery.ReadRow();

                    // 파일이 더 이상 없거나, 읽을 데이터가 없으면 종료
                    if (rawLine == null)
                    {
                        MessageBox.Show("BigQuery Playback End");
                        break;
                    }

                    // 빈 줄 또는 헤더는 건너뜀
                    if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith("Timestamp,"))
                        continue;

                    // 첫 번째 열(timestamp)와 나머지(row) 분리
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

                    OnMessageReceived?.Invoke(row);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("BigQuery Playback Error: " + ex.Message);
            }

            OnFinished?.Invoke();
        }

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

                        uint acio = OnMessageReceived?.Invoke(rawLine) ?? 0;
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

            bigQuery?.WriteRow(timestamp, msg);

            uint acio = OnMessageReceived?.Invoke(msg) ?? 0;
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
