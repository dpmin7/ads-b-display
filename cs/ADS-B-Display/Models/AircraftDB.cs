using ADS_B_Display.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ADS_B_Display
{

    public static class AircraftDB
    {
        private static Dictionary<uint, Dictionary<string, string>> aircrafts = new Dictionary<uint, Dictionary<string, string>>();

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
        new ICAORange(0x008000, 0x00FFFF, "ZA", "South Africa"),
        new ICAORange(0x010000, 0x017FFF, "EG", "Egypt"),
        new ICAORange(0x018000, 0x01FFFF, "LY", "Libya"),
        new ICAORange(0x020000, 0x027FFF, "MA", "Morocco"),
        new ICAORange(0x028000, 0x02FFFF, "TN", "Tunisia"),
        new ICAORange(0x030000, 0x0303FF, "BW", "Botswana"),
        new ICAORange(0x030400, 0x0307FF, "BI", "Burundi"),
        new ICAORange(0x034000, 0x034FFF, "CM", "Cameroon"),
        new ICAORange(0x035000, 0x0353FF, "KM", "Comoros"),
        new ICAORange(0x036000, 0x036FFF, "CG", "Congo"),
        new ICAORange(0x038000, 0x038FFF, "CI", "Côte d'Ivoire"),
        new ICAORange(0x040000, 0x0403FF, "GA", "Gabon"),
        new ICAORange(0x044000, 0x044FFF, "GH", "Ghana"),
        new ICAORange(0x048000, 0x048FFF, "GN", "Guinea"),
        new ICAORange(0x04C000, 0x04CFFF, "KE", "Kenya"),
        new ICAORange(0x050000, 0x050FFF, "LR", "Liberia"),
        new ICAORange(0x054000, 0x054FFF, "MG", "Madagascar"),
        new ICAORange(0x058000, 0x058FFF, "MW", "Malawi"),
        new ICAORange(0x05C000, 0x05CFFF, "MV", "Maldives"),
        new ICAORange(0x060000, 0x0603FF, "MR", "Mauritania"),
        new ICAORange(0x062000, 0x062FFF, "NE", "Niger"),
        new ICAORange(0x064000, 0x064FFF, "NG", "Nigeria"),
        new ICAORange(0x068000, 0x068FFF, "UG", "Uganda"),
        new ICAORange(0x06A000, 0x06A3FF, "QA", "Qatar"),
        new ICAORange(0x06C000, 0x06C3FF, "CF", "Central African Republic"),
        new ICAORange(0x06E000, 0x06E3FF, "RW", "Rwanda"),
        new ICAORange(0x070000, 0x070FFF, "SN", "Senegal"),
        new ICAORange(0x074000, 0x0743FF, "SC", "Seychelles"),
        new ICAORange(0x076000, 0x0763FF, "SL", "Sierra Leone"),
        new ICAORange(0x078000, 0x078FFF, "SO", "Somalia"),
        new ICAORange(0x080000, 0x080FFF, "TZ", "Tanzania"),
        new ICAORange(0x084000, 0x084FFF, "TD", "Chad"),
        new ICAORange(0x088000, 0x088FFF, "TG", "Togo"),
        new ICAORange(0x090000, 0x0903FF, "CD", "Democratic Republic of Congo"),
        new ICAORange(0x094000, 0x094FFF, "ZM", "Zambia"),
        new ICAORange(0x098000, 0x0983FF, "GQ", "Equatorial Guinea"),
        new ICAORange(0x0A0000, 0x0A7FFF, "DZ", "Algeria"),
        new ICAORange(0x0A8000, 0x0A8FFF, "BS", "Bahamas"),
        new ICAORange(0x0A9000, 0x0A93FF, "BB", "Barbados"),
        new ICAORange(0x0AA000, 0x0AA3FF, "BZ", "Belize"),
        new ICAORange(0x0AB000, 0x0ABFFF, "CO", "Colombia"),
        new ICAORange(0x0AC000, 0x0ACFFF, "CR", "Costa Rica"),
        new ICAORange(0x0AD000, 0x0ADFFF, "CU", "Cuba"),
        new ICAORange(0x0AE000, 0x0AEFFF, "SV", "El Salvador"),
        new ICAORange(0x0AF000, 0x0AFFFF, "GT", "Guatemala"),
        new ICAORange(0x0B1000, 0x0B1FFF, "HN", "Honduras"),
        new ICAORange(0x0B3000, 0x0B3FFF, "JM", "Jamaica"),
        new ICAORange(0x0B5000, 0x0B5FFF, "NI", "Nicaragua"),
        new ICAORange(0x0B7000, 0x0B7FFF, "PA", "Panama"),
        new ICAORange(0x0B8000, 0x0B8FFF, "DO", "Dominican Republic"),
        new ICAORange(0x0B9000, 0x0B9FFF, "HT", "Haiti"),
        new ICAORange(0x0BA000, 0x0BAFFF, "TT", "Trinidad and Tobago"),
        new ICAORange(0x0BB000, 0x0BBFFF, "SR", "Suriname"),
        new ICAORange(0x0D0000, 0x0D7FFF, "PE", "Peru"),
        new ICAORange(0x0D8000, 0x0DFFFF, "VE", "Venezuela"),
        new ICAORange(0x100000, 0x1FFFFF, "RU", "Russian Federation"),
        new ICAORange(0x201000, 0x2013FF, "NA", "Namibia"),
        new ICAORange(0x300000, 0x33FFFF, "IT", "Italy"),
        new ICAORange(0x340000, 0x37FFFF, "ES", "Spain"),
        new ICAORange(0x380000, 0x3BFFFF, "FR", "France"),
        new ICAORange(0x3C0000, 0x3FFFFF, "DE", "Germany"),
        new ICAORange(0x400000, 0x43FFFF, "GB", "United Kingdom"),
        new ICAORange(0x440000, 0x447FFF, "AT", "Austria"),
        new ICAORange(0x448000, 0x44FFFF, "BE", "Belgium"),
        new ICAORange(0x450000, 0x457FFF, "BG", "Bulgaria"),
        new ICAORange(0x458000, 0x45FFFF, "DK", "Denmark"),
        new ICAORange(0x460000, 0x467FFF, "FI", "Finland"),
        new ICAORange(0x468000, 0x46FFFF, "GR", "Greece"),
        new ICAORange(0x470000, 0x477FFF, "HU", "Hungary"),
        new ICAORange(0x478000, 0x47FFFF, "NO", "Norway"),
        new ICAORange(0x480000, 0x487FFF, "NL", "Netherlands"),
        new ICAORange(0x488000, 0x48FFFF, "PL", "Poland"),
        new ICAORange(0x490000, 0x497FFF, "PT", "Portugal"),
        new ICAORange(0x498000, 0x49FFFF, "CZ", "Czech Republic"),
        new ICAORange(0x4A0000, 0x4A7FFF, "RO", "Romania"),
        new ICAORange(0x4A8000, 0x4AFFFF, "SE", "Sweden"),
        new ICAORange(0x4B0000, 0x4B7FFF, "CH", "Switzerland"),
        new ICAORange(0x4B8000, 0x4BFFFF, "TR", "Turkey"),
        new ICAORange(0x4C0000, 0x4C7FFF, "RS", "Serbia"),
        new ICAORange(0x4C8000, 0x4C83FF, "CY", "Cyprus"),
        new ICAORange(0x4CA000, 0x4CAFFF, "IE", "Ireland"),
        new ICAORange(0x4CB000, 0x4CBFFF, "IS", "Iceland"),
        new ICAORange(0x4CC000, 0x4CC3FF, "LU", "Luxembourg"),
        new ICAORange(0x4CD000, 0x4CD3FF, "MT", "Malta"),
        new ICAORange(0x4CD400, 0x4CD7FF, "MC", "Monaco"),
        new ICAORange(0x501000, 0x5013FF, "AL", "Albania"),
        new ICAORange(0x501C00, 0x501FFF, "HR", "Croatia"),
        new ICAORange(0x502C00, 0x502FFF, "LV", "Latvia"),
        new ICAORange(0x503C00, 0x503FFF, "LT", "Lithuania"),
        new ICAORange(0x504C00, 0x504FFF, "MD", "Moldova"),
        new ICAORange(0x505C00, 0x505FFF, "SK", "Slovakia"),
        new ICAORange(0x506C00, 0x506FFF, "SI", "Slovenia"),
        new ICAORange(0x507C00, 0x507FFF, "UZ", "Uzbekistan"),
        new ICAORange(0x508000, 0x50FFFF, "UA", "Ukraine"),
        new ICAORange(0x510000, 0x5103FF, "BY", "Belarus"),
        new ICAORange(0x511000, 0x5113FF, "EE", "Estonia"),
        new ICAORange(0x512000, 0x5123FF, "MK", "North Macedonia"),
        new ICAORange(0x513000, 0x5133FF, "BA", "Bosnia and Herzegovina"),
        new ICAORange(0x514000, 0x5143FF, "GE", "Georgia"),
        new ICAORange(0x515000, 0x5153FF, "TJ", "Tajikistan"),
        new ICAORange(0x600000, 0x6003FF, "AM", "Armenia"),
        new ICAORange(0x600800, 0x600BFF, "AZ", "Azerbaijan"),
        new ICAORange(0x601000, 0x6013FF, "KG", "Kyrgyzstan"),
        new ICAORange(0x601800, 0x601BFF, "TM", "Turkmenistan"),
        new ICAORange(0x680000, 0x6803FF, "KZ", "Kazakhstan"),
        new ICAORange(0x700000, 0x700FFF, "AF", "Afghanistan"),
        new ICAORange(0x708000, 0x708FFF, "BD", "Bangladesh"),
        new ICAORange(0x710000, 0x710FFF, "MM", "Myanmar"),
        new ICAORange(0x718000, 0x718FFF, "KP", "North Korea (DPRK)"),
        new ICAORange(0x720000, 0x7203FF, "CK", "Cook Islands"),
        new ICAORange(0x738000, 0x7383FF, "FJ", "Fiji"),
        new ICAORange(0x750000, 0x757FFF, "ID", "Indonesia"),
        new ICAORange(0x758000, 0x75FFFF, "IR", "Iran"),
        new ICAORange(0x760000, 0x767FFF, "IQ", "Iraq"),
        new ICAORange(0x768000, 0x76FFFF, "IL", "Israel"),
        new ICAORange(0x770000, 0x770FFF, "JO", "Jordan"),
        new ICAORange(0x778000, 0x77FFFF, "LB", "Lebanon"),
        new ICAORange(0x780000, 0x7BFFFF, "CN", "China"),
        new ICAORange(0x7C0000, 0x7FFFFF, "AU", "Australia"),
        new ICAORange(0x800000, 0x83FFFF, "IN", "India"),
        new ICAORange(0x840000, 0x87FFFF, "JP", "Japan"),
        new ICAORange(0x880000, 0x887FFF, "KR", "Republic of Korea"),
        new ICAORange(0x888000, 0x888FFF, "PK", "Pakistan"),
        new ICAORange(0x890000, 0x890FFF, "PH", "Philippines"),
        new ICAORange(0x894000, 0x8943FF, "SG", "Singapore"),
        new ICAORange(0x895000, 0x895FFF, "LK", "Sri Lanka"),
        new ICAORange(0x896000, 0x896FFF, "SY", "Syria"),
        new ICAORange(0x8A0000, 0x8A7FFF, "TH", "Thailand"),
        new ICAORange(0x8A8000, 0x8A83FF, "BN", "Brunei Darussalam"),
        new ICAORange(0xA00000, 0xAFFFFF, "US", "United States"),
        new ICAORange(0xC00000, 0xC3FFFF, "CA", "Canada"),
        new ICAORange(0xC80000, 0xC87FFF, "NZ", "New Zealand"),
        new ICAORange(0xC88000, 0xC88FFF, "PG", "Papua New Guinea"),
        new ICAORange(0xE00000, 0xE3FFFF, "AR", "Argentina"),
        new ICAORange(0xE40000, 0xE7FFFF, "BR", "Brazil"),
        new ICAORange(0xE80000, 0xEBFFFF, "CL", "Chile"),
        new ICAORange(0xEC0000, 0xEC0FFF, "EC", "Ecuador"),
        new ICAORange(0xED0000, 0xED0FFF, "PY", "Paraguay"),
        new ICAORange(0xEE0000, 0xEE0FFF, "UY", "Uruguay"),
        new ICAORange(0xF00000, 0xF07FFF, "SA", "Saudi Arabia"),
        new ICAORange(0xF08000, 0xF083FF, "BH", "Bahrain"),
        new ICAORange(0xF09000, 0xF093FF, "OM", "Oman"),
        new ICAORange(0xF0A000, 0xF0A3FF, "AE", "United Arab Emirates"),
        new ICAORange(0xF0D000, 0xF0D3FF, "KW", "Kuwait"),
        new ICAORange(0x000001, 0x000001, "ICAO", "ICAO Test"), // ICAO Test Address
        new ICAORange(0x899000, 0x8993FF, "ICAO", "ICAO (AVS)"),
        new ICAORange(0xADF800, 0xADF800, "ICAO", "ICAO (TIS-B)"),
    };

        private static readonly ICAORange[] MilitaryRanges = new ICAORange[]
        {
            new ICAORange(0xADF7C8, 0xAFFFFF, "US", "United States Military"),
            new ICAORange(0x3B0000, 0x3BFFFF, "DE", "Germany Military"),
            new ICAORange(0xC0CDF9, 0xC3FFFF, "CA", "Canada Military")
            // 생략된 범위는 필요 시 추가
        };

        static AircraftDB ()
        {
            Task.Run(() =>
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AircraftDB", "aircraftDatabase.csv");

                if (!File.Exists(fullPath))
                    return;

                string data = File.ReadAllText(fullPath);

                List<Dictionary<string, string>> parseData = CsvUtil.Parse(data);

                foreach (var row in parseData)
                {
                    if (row.TryGetValue("icao24", out string icaoStr))
                    {
                        Console.WriteLine($"icaoStr: {icaoStr}");
                        // ICAO 값은 일반적으로 16진수 문자열 (ex: "a3c2f7") → uint로 변환 필요
                        if (uint.TryParse(icaoStr, System.Globalization.NumberStyles.HexNumber, null, out uint icaoKey))
                        {
                            // 중복 ICAO는 덮어쓰기됨
                            aircrafts[icaoKey] = row;
                        }
                        else
                        {
                            Console.WriteLine($"ICAO 값 파싱 실패: {icaoStr}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("icao24 필드가 없는 row 발견");
                    }

                }

                Console.WriteLine("end!!");
            });
        }

        public static Dictionary<string, string> GetAircraftInfo(uint addr)
        {
            if (aircrafts != null && aircrafts.Count > 0 && aircrafts.TryGetValue(addr, out var data)) {
                var deepCopy = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    JsonConvert.SerializeObject(data)
                );

                deepCopy.Add("country", GetCountry(addr, false));

                return deepCopy;
            }

            return null;
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
            if (aircrafts.TryGetValue(addr, out var data)) {
                var type = data["icaoaircrafttype"];
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
