using ADS_B_Display.Models;
using ADS_B_Display.Models.Connector;
using ADS_B_Display.Models.Parser;
using ADS_B_Display.Models.Recorder;
using ADS_B_Display.Views;
using ADS_B_Display.Utils;
using NLog;
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
    public enum MsgType { SbsMsg, RawMsg }
    public enum ConnectorType { TCP, File, DB }
    public class SbsWorker
    {
        public ConnectorType ConnectorType { get; private set; }

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        enum MsgType { SbsMsg, RawMsg }
        private IConnector _inputSource;
        private InputSourceState _state = new InputSourceState();
        private IRecorder _recorder;

        private Thread _thread;
        private bool _first = true;
        private long _lastTime;
        private int _playBackSpeed = 1;

        private readonly Func<string, long, uint> _userOnMessageReceived;
        private Func<string, long, uint> _internalOnMessageReceived;
        private Action OnFinished;

        public StreamWriter RecordStream;
        private bool _useDb = false;
        private IDBConnector _dbConnector;

        public SbsWorker(IParser parser)
        {
            //_userOnMessageReceived = onMessageReceived;
            _internalOnMessageReceived = (msg, ts) =>
            {
                RecordMessage(ts, msg); // 항상 먼저 기록

                if (AirScreenPanelView.msgReceiveTime == null)
                {
                    logger.Info($"[SBS] First Message: {DateTime.Now:HH:mm:ss.fff}");
                    AirScreenPanelView.msgReceiveTime = DateTime.Now;
                }

                uint acio = parser.Parse(msg, ts);
                PostProcess(acio);

                return acio;
            };
        }

        public bool Start(string path, bool useDb = false, IDBConnector dbConnector = null)
        {
            try
            {
                _state.Running = true;
                _state.First = true;
                _state.LastTime = 0;

                AircraftManager.PurgeAll();

                if (useDb && dbConnector != null)
                {
                    _inputSource = new DatabaseConnector(dbConnector);
                    ConnectorType = ConnectorType.DB;
                }
                else
                {
                    _inputSource = new FileConnector(path);
                    ConnectorType = ConnectorType.File;
                }

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
            if (_inputSource == null)
            {
                _playBackSpeed = speed;
            }
            else
            {
                _inputSource.SetPlaybackSpeed(speed);
            }
        }

        public async Task<bool> Start(string host, int port, CancellationToken ct)
        {
            try
            {
                _state.Running = true;
                _state.First = true;
                _state.LastTime = 0;

                AircraftManager.PurgeAll();

                _inputSource = new TcpConnector(host, port);
                ConnectorType = ConnectorType.TCP;
                
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
            _state.Running = false;
            _inputSource?.Stop();
            _inputSource?.Dispose();
            _inputSource = null;
        }

        private void PostProcess(uint acio)
        {
            if (AircraftManager.TryGet(acio, out var aircraft))
            {
                if (aircraft.HaveLatLon)
                {
                    var curr = new AircraftTrackPoint(aircraft.Latitude, aircraft.Longitude, aircraft.Altitude, aircraft.LastSeen);

                    if (aircraft.TrackPoint.Items.Count >= 1)
                    {

                        var prev = aircraft.TrackPoint.Items.ElementAt(0);

                        aircraft.Speed = AircraftTrackUtils.CalculateSpeedKnots(prev, curr);
                        //oldLatitude = aircraft.TrackPoint.Items.ElementAt(0).Latitude;
                        //oldLongitude = aircraft.TrackPoint.Items.ElementAt(0).Longitude;
                        //if (oldLatitude == aircraft.Latitude && oldLongitude == aircraft.Longitude)
                        //{
                        //    aircraft.Speed = 0;
                        //}
                    }
                       
                    aircraft.AddTrackPoint(curr);
                }
            }
        }
    }

    public static class AircraftTrackUtils
    {
        private const double EarthRadiusKm = 6371.0;

        public static double CalculateSpeedKnots(AircraftTrackPoint prev, AircraftTrackPoint current)
        {
            double distanceKm = HaversineDistanceKm(
                prev.Latitude, prev.Longitude,
                current.Latitude, current.Longitude
            );

            double timeSeconds = (current.TimestampUtc - prev.TimestampUtc) / 1000.0;

            if (timeSeconds <= 0.0)
                return 0.0;

            double speedKmh = (distanceKm / timeSeconds) * 3600.0;
            double speedKnots = speedKmh * 0.539957;

            return speedKnots;
        }

        private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double toRad = Math.PI / 180.0;

            double dLat = (lat2 - lat1) * toRad;
            double dLon = (lon2 - lon1) * toRad;

            double a = Math.Pow(Math.Sin(dLat / 2), 2) +
                       Math.Cos(lat1 * toRad) * Math.Cos(lat2 * toRad) *
                       Math.Pow(Math.Sin(dLon / 2), 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c;
        }
    }
}
