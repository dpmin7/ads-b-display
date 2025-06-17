using System;
using System.Collections.Generic;
using System.Globalization;

namespace ADS_B_Display
{
    public class AircraftData
    {
        public uint ICAO24;
        public string[] Fields = new string[27];
    }

    public static class AircraftDB
    {
        private const int AC_DB_NUM_FIELDS = 27;
        private const int AC_DB_ICAOAircraftType = 8;

        private static readonly Dictionary<uint, AircraftData> _hashTable = new Dictionary<uint, AircraftData>(600000);
        private static readonly string[] DefaultFields = CreateDefaultFields();
        private static AircraftData _record = new AircraftData { Fields = CreateDefaultFields() };

        private static readonly string[] HelicopterTypes = { "H1P", "H2P", "H1T", "H2T" };

        private struct ICAORange
        {
            public uint Low, High;
            public string ShortCode, LongName;

            public ICAORange(uint low, uint high, string shortCode, string longName)
            {
                Low = low; High = high;
                ShortCode = shortCode; LongName = longName;
            }
        }

        private static readonly ICAORange[] CountryRanges = new ICAORange[]
        {
            new ICAORange(0x004000, 0x0043FF, "ZW", "Zimbabwe"),
            new ICAORange(0xA00000, 0xAFFFFF, "US", "United States"),
            new ICAORange(0xC00000, 0xC3FFFF, "CA", "Canada"),
            new ICAORange(0x440000, 0x447FFF, "AT", "Austria"),
            new ICAORange(0x400000, 0x43FFFF, "GB", "United Kingdom")
            // 생략된 범위는 필요 시 추가
        };

        private static readonly ICAORange[] MilitaryRanges = new ICAORange[]
        {
            new ICAORange(0xADF7C8, 0xAFFFFF, "US", "United States Military"),
            new ICAORange(0x3B0000, 0x3BFFFF, "DE", "Germany Military"),
            new ICAORange(0xC0CDF9, 0xC3FFFF, "CA", "Canada Military")
            // 생략된 범위는 필요 시 추가
        };

        public static bool Init(string filePath)
        {
            var ctx = new CsvContext {
                FileName = filePath + "\\aircraftDatabase.csv",
                Delimiter = ',',
                LineSize = 2000,
                Callback = CSV_Callback
            };

            Console.WriteLine("Reading Aircraft DB");
            var parser = new CsvParser(ctx);
            if (parser.Parse() == 0) {
                Console.WriteLine($"Parsing of \"{filePath}\" failed");
                return false;
            }

            Console.WriteLine("Done Reading Aircraft DB");
            return true;
        }

        private static string[] CreateDefaultFields()
        {
            var fields = new string[AC_DB_NUM_FIELDS];
            for (int i = 0; i < fields.Length; i++)
                fields[i] = "?";
            return fields;
        }

        private static bool CSV_Callback(CsvContext ctx, string value)
        {
            if (!string.IsNullOrEmpty(value)) {
                _record.Fields[ctx.FieldNum] = value;
                if (ctx.FieldNum == 0) {
                    try {
                        _record.ICAO24 = uint.Parse(value, NumberStyles.HexNumber);
                    } catch { _record.ICAO24 = 0; }
                }
            }
            
            if (ctx.FieldNum == ctx.NumFields - 1) {
                if (_record.ICAO24 != 0) {
                    if (_hashTable.ContainsKey(_record.ICAO24)) {
                        Console.WriteLine($"Duplicate Aircraft Record {_record.Fields[0]} {_record.ICAO24:X}");
                    } else {
                        var copy = new AircraftData { ICAO24 = _record.ICAO24, Fields = (string[])_record.Fields.Clone() };
                        _hashTable.Add(copy.ICAO24, copy);
                    }
                }
                for (int i = 0; i < AC_DB_NUM_FIELDS; i++)
                    _record.Fields[i] = "?";
            }
            return true;
        }

        public static string GetAircraftInfo(uint addr)
        {
            if (_hashTable.TryGetValue(addr, out var data)) {
                bool isHelo = IsHelicopter(addr, out var type);
                bool isMil = IsMilitary(addr, out var countryCode);

                return string.Format("addr:0x{0:X6}, Reg:{1}, Model:{2}, ICAO-Type:{3}, Country:{4}{5}{6}",
                    data.ICAO24,
                    data.Fields[1],
                    data.Fields[4],
                    data.Fields[AC_DB_ICAOAircraftType],
                    GetCountry(addr, false),
                    isMil ? " (Military)" : "",
                    isHelo ? $" (Heli:{type})" : "");
            }
            return $"addr: 0x{addr:X6}, No Data";
        }

        public static string GetCountry(uint addr, bool shortCode)
        {
            foreach (var range in CountryRanges) {
                if (addr >= range.Low && addr <= range.High)
                    return shortCode ? range.ShortCode : range.LongName;
            }
            return "Unknown";
        }

        public static bool IsMilitary(uint addr, out string countryCode)
        {
            foreach (var range in MilitaryRanges) {
                if (addr >= range.Low && addr <= range.High) {
                    countryCode = range.ShortCode;
                    return true;
                }
            }
            countryCode = null;
            return false;
        }

        public static bool IsHelicopter(uint addr, out string typeCode)
        {
            typeCode = null;
            if (_hashTable.TryGetValue(addr, out var data)) {
                var type = data.Fields[AC_DB_ICAOAircraftType];
                if (!string.IsNullOrEmpty(type) && type.StartsWith("H")) {
                    foreach (var h in HelicopterTypes) {
                        if (string.Equals(h, type, StringComparison.OrdinalIgnoreCase)) {
                            typeCode = h;
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
