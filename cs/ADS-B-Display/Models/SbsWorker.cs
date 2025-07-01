using ADS_B_Display.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive;
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
        private IConnector _inputSource;
        private InputSourceState _state = new InputSourceState();
        private IRecorder _recorder;

        private Thread _thread;
        private volatile bool _running;
        private bool _first = true;
        private long _lastTime;
        private int _playBackSpeed = 1;

        private readonly Func<string, long, uint> _userOnMessageReceived;
        private Func<string, long, uint> _internalOnMessageReceived;
        private Action OnFinished;

        public StreamWriter RecordStream;
        private bool _useDb = false;
        private IDBConnector _dbConnector;

        public SbsWorker(Func<string, long, uint> onMessageReceived)
        {
            _userOnMessageReceived = onMessageReceived;
            _internalOnMessageReceived = (msg, ts) =>
            {
                RecordMessage(ts, msg); // 항상 먼저 기록

                uint acio = _userOnMessageReceived?.Invoke(msg, ts) ?? 0;
                PostProcess(acio);

                return _userOnMessageReceived?.Invoke(msg, ts) ?? 0;
            };
        }

        public bool Start(string path, bool useDb = false, IDBConnector dbConnector = null)
        {
            try
            {
                _state.Running = true;
                _state.First = true;
                _state.LastTime = 0;
                if (useDb && dbConnector != null)
                    _inputSource = new DatabaseConnector(dbConnector);
                else
                    _inputSource = new FileConnector(path);

                _inputSource.Start(_internalOnMessageReceived, _playBackSpeed, _state);
                return true;
            }
            catch
            {
                return false;
            }

            _thread?.Join();
        }
        public void setPlayBackSpeed(int speed)
        {
            _playBackSpeed = speed;
        }

        public async Task<bool> Start(string host, int port, CancellationToken ct)
        {
            try
            {
                _state.Running = true;
                _state.First = true;
                _state.LastTime = 0;
                _inputSource = new TcpConnector(host, port);
                await _inputSource.StartAsync(_internalOnMessageReceived, _playBackSpeed, ct, _state);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RecordOn(string path, bool useDb = false, IDBConnector dbConnector = null)
        {
            if (_recorder != null)
                _recorder.Dispose();

            if (useDb)
            {
                if (dbConnector == null)
                {
                    Console.WriteLine("DB Connector is not initialized.");
                    return;
                }
                _recorder = new DbRecorder();
                _recorder.Start(null, dbConnector);
            }
            else
            {
                _recorder = new FileRecorder();
                _recorder.Start(path);
            }
        }

        public void RecordOff()
        {
            _recorder?.Stop();
            _recorder = null;
        }

        private void RecordMessage(long timestamp, string row)
        {
            _recorder?.Write(timestamp, row);
        }

        public void Stop(bool useDb = false)
        {
            RecordOff();
            _running = false;
            _inputSource?.Stop();
            _inputSource?.Dispose();
            _inputSource = null;
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
