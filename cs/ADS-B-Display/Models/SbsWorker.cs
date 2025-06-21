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
        private string _filePath;
        private TcpClient _tcpClient;
        private Thread _thread;
        private volatile bool _running;
        private bool _first = true;
        private long _lastTime;
        private int _playBackSpeed = 1;

        private Action<string> OnMessageReceived;
        private Action OnFinished;

        public StreamWriter RecordStream;
        public StreamWriter BigQueryCsv;
        public int BigQueryRowCount = 0;
        public int BigQueryUploadThreshold = 1000;
        public string BigQueryScriptPath;
        public string BigQueryCsvFileName;
        public string BigQueryUploadArgs;

        public SbsWorker(Action<string> onMessageReceived)
        {
            OnMessageReceived = onMessageReceived;
        }

        public bool Start(string path)
        {
            try {
                _first = true;
                _running = true;
                _filePath = path;
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

        public void RecordOn(string path)
        {
            RecordStream = new StreamWriter(path, append: true) {
                AutoFlush = true
            };
        }

        public void RecordOff()
        {
            try {
                RecordStream?.Flush();
                RecordStream?.Close();
                RecordStream = null;
            } catch (Exception ex) {
                MessageBox.Show($"SBS 기록 파일을 닫는 동안 오류가 발생했습니다:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Stop()
        {   
            RecordOff(); // 레코딩 중에 멈추면 레코딩 종료부터 하자.
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
            if (_filePath != null)
                RunFileMode();
            else if (_tcpClient != null)
                RunTcpMode();
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
                        OnMessageReceived?.Invoke(rawLine);
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
                        if (msg == null) break;
                        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        ProcessMessage(msg, time);
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show("TCP Error: " + ex.Message);
            }

            OnFinished?.Invoke();
        }

        private void ProcessMessage(string msg, long timestamp)
        {
            RecordStream?.WriteLine(timestamp);
            RecordStream?.WriteLine(msg);

            if (BigQueryCsv != null) {
                BigQueryCsv.WriteLine(msg);
                BigQueryRowCount++;
                if (BigQueryRowCount >= BigQueryUploadThreshold) {
                    BigQueryCsv.Close();
                    RunBigQueryUpload();
                    CreateNewBigQueryCsv();
                }
            }

            OnMessageReceived?.Invoke(msg);
        }

        private void RunBigQueryUpload()
        {
            try {
                var psi = new System.Diagnostics.ProcessStartInfo() {
                    FileName = "python",
                    Arguments = $"\"{BigQueryScriptPath}\" {BigQueryUploadArgs} \"{BigQueryCsvFileName}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi);
            } catch (Exception ex) {
                MessageBox.Show("BigQuery Upload Error: " + ex.Message);
            }
        }

        private void CreateNewBigQueryCsv()
        {
            BigQueryRowCount = 0;
            BigQueryCsv = new StreamWriter(BigQueryCsvFileName, false);
        }
    }
}
