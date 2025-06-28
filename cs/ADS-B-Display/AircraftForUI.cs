using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ADS_B_Display
{
    public class AircraftForUI : NotifyPropertyChangedBase
    {
        public void UpdateAircraftForUI(Aircraft aircraft)
        {
            ICAO = aircraft.ICAO;
            HexAddr = aircraft.HexAddr;
            LastSeen = aircraft.LastSeen;
            NumMessagesRaw = aircraft.NumMessagesRaw;
            NumMessagesSBS = aircraft.NumMessagesSBS;
            Odd_cprlat = aircraft.odd_cprlat;
            Odd_cprlon = aircraft.odd_cprlon;
            Even_cprlat = aircraft.even_cprlat;
            Even_cprlon = aircraft.even_cprlon;
            Odd_cprtime = aircraft.odd_cprtime;
            Even_cprtime = aircraft.even_cprtime;
            FlightNum = aircraft.FlightNum;
            HaveFlightNum = aircraft.HaveFlightNum;
            HaveAltitude = aircraft.HaveAltitude;
            Altitude = aircraft.Altitude;
            HaveLatLon = aircraft.HaveLatLon;
            Latitude = aircraft.Latitude;
            Longitude = aircraft.Longitude;
            HaveSpeedAndHeading = aircraft.HaveSpeedAndHeading;
            Heading = aircraft.Heading;
            Speed = aircraft.Speed;
            VerticalRate = aircraft.VerticalRate;
            SpriteImage = aircraft.SpriteImage;
        }

        private uint _icao;
        public uint ICAO
        {
            get => _icao;
            set => SetProperty(ref _icao, value);
        }

        private string _hexAddr = new string('\0', 6);
        public string HexAddr
        {
            get => _hexAddr;
            set => SetProperty(ref _hexAddr, value);
        }

        private long _lastSeen;
        public long LastSeen
        {
            get => _lastSeen;
            set => SetProperty(ref _lastSeen, value);
        }

        private long _numMessagesRaw;
        public long NumMessagesRaw
        {
            get => _numMessagesRaw;
            set => SetProperty(ref _numMessagesRaw, value);
        }

        private long _numMessagesSBS;
        public long NumMessagesSBS
        {
            get => _numMessagesSBS;
            set => SetProperty(ref _numMessagesSBS, value);
        }

        private int _oddCprLat;
        public int Odd_cprlat
        {
            get => _oddCprLat;
            set => SetProperty(ref _oddCprLat, value);
        }

        private int _oddCprLon;
        public int Odd_cprlon
        {
            get => _oddCprLon;
            set => SetProperty(ref _oddCprLon, value);
        }

        private int _evenCprLat;
        public int Even_cprlat
        {
            get => _evenCprLat;
            set => SetProperty(ref _evenCprLat, value);
        }

        private int _evenCprLon;
        public int Even_cprlon
        {
            get => _evenCprLon;
            set => SetProperty(ref _evenCprLon, value);
        }

        private long _oddCprTime;
        public long Odd_cprtime
        {
            get => _oddCprTime;
            set => SetProperty(ref _oddCprTime, value);
        }

        private long _evenCprTime;
        public long Even_cprtime
        {
            get => _evenCprTime;
            set => SetProperty(ref _evenCprTime, value);
        }

        private string _flightNum = string.Empty;
        public string FlightNum
        {
            get => _flightNum;
            set => SetProperty(ref _flightNum, value);
        }

        private bool _haveFlightNum;
        public bool HaveFlightNum
        {
            get => _haveFlightNum;
            set => SetProperty(ref _haveFlightNum, value);
        }

        private bool _haveAltitude;
        public bool HaveAltitude
        {
            get => _haveAltitude;
            set => SetProperty(ref _haveAltitude, value);
        }

        private double _altitude;
        public double Altitude
        {
            get => _altitude;
            set => SetProperty(ref _altitude, value);
        }

        private bool _haveLatLon;
        public bool HaveLatLon
        {
            get => _haveLatLon;
            set => SetProperty(ref _haveLatLon, value);
        }

        private double _latitude;
        public double Latitude
        {
            get => _latitude;
            set => SetProperty(ref _latitude, value);
        }

        private double _longitude;
        public double Longitude
        {
            get => _longitude;
            set => SetProperty(ref _longitude, value);
        }

        private bool _haveSpeedAndHeading;
        public bool HaveSpeedAndHeading
        {
            get => _haveSpeedAndHeading;
            set => SetProperty(ref _haveSpeedAndHeading, value);
        }

        private double _heading;
        public double Heading
        {
            get => _heading;
            set => SetProperty(ref _heading, value);
        }

        private double _speed;
        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        private double _verticalRate;
        public double VerticalRate
        {
            get => _verticalRate;
            set => SetProperty(ref _verticalRate, value);
        }

        private int _spriteImage;
        public int SpriteImage
        {
            get => _spriteImage;
            set => SetProperty(ref _spriteImage, value);
        }
    }
}
