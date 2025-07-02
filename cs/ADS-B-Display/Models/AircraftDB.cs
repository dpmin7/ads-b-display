using ADS_B_Display.Models;
using ADS_B_Display.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ADS_B_Display
{

    public static class AircraftDB
    {
        private static Dictionary<uint, AircraftData> _aircraftDataTable;

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
            // --- Africa ---
            new ICAORange(0x004000, 0x0043FF, "ZW", "Zimbabwe"),
            new ICAORange(0x006000, 0x006FFF, "MZ", "Mozambique"),
            new ICAORange(0x008000, 0x00FFFF, "ZA", "South Africa"),
            new ICAORange(0x010000, 0x017FFF, "EG", "Egypt"),
            new ICAORange(0x018000, 0x01FFFF, "LY", "Libya"),
            new ICAORange(0x020000, 0x027FFF, "MA", "Morocco"),
            new ICAORange(0x028000, 0x02FFFF, "TN", "Tunisia"),
            new ICAORange(0x030000, 0x0303FF, "BW", "Botswana"),
            new ICAORange(0x032000, 0x032FFF, "BI", "Burundi"),
            new ICAORange(0x034000, 0x034FFF, "CM", "Cameroon"),
            new ICAORange(0x035000, 0x0353FF, "KM", "Comoros"),
            new ICAORange(0x036000, 0x036FFF, "CG", "Congo"),
            new ICAORange(0x038000, 0x038FFF, "CI", "Cote d'Ivoire"),
            new ICAORange(0x03E000, 0x03EFFF, "GA", "Gabon"),
            new ICAORange(0x040000, 0x040FFF, "ET", "Ethiopia"),
            new ICAORange(0x042000, 0x042FFF, "GQ", "Equatorial Guinea"),
            new ICAORange(0x044000, 0x044FFF, "GH", "Ghana"),
            new ICAORange(0x046000, 0x046FFF, "GN", "Guinea"),
            new ICAORange(0x048000, 0x0483FF, "GW", "Guinea-Bissau"),
            new ICAORange(0x04A000, 0x04A3FF, "LS", "Lesotho"),
            new ICAORange(0x04C000, 0x04CFFF, "KE", "Kenya"),
            new ICAORange(0x050000, 0x050FFF, "LR", "Liberia"),
            new ICAORange(0x054000, 0x054FFF, "MG", "Madagascar"),
            new ICAORange(0x058000, 0x058FFF, "MW", "Malawi"),
            new ICAORange(0x05A000, 0x05A3FF, "MV", "Maldives"),
            new ICAORange(0x05C000, 0x05CFFF, "ML", "Mali"),
            new ICAORange(0x05E000, 0x05E3FF, "MR", "Mauritania"),
            new ICAORange(0x060000, 0x0603FF, "MU", "Mauritius"),
            new ICAORange(0x062000, 0x062FFF, "NE", "Niger"),
            new ICAORange(0x064000, 0x064FFF, "NG", "Nigeria"),
            new ICAORange(0x068000, 0x068FFF, "UG", "Uganda"),
            new ICAORange(0x06A000, 0x06A3FF, "QA", "Qatar"),
            new ICAORange(0x06C000, 0x06CFFF, "CF", "Central African Republic"),
            new ICAORange(0x06E000, 0x06EFFF, "RW", "Rwanda"),
            new ICAORange(0x070000, 0x070FFF, "SN", "Senegal"),
            new ICAORange(0x074000, 0x0743FF, "SC", "Seychelles"),
            new ICAORange(0x076000, 0x0763FF, "SL", "Sierra Leone"),
            new ICAORange(0x07A000, 0x07A3FF, "SZ", "Eswatini"), // Swaziland -> Eswatini
            new ICAORange(0x07C000, 0x07CFFF, "SD", "Sudan"),
            new ICAORange(0x080000, 0x080FFF, "TZ", "Tanzania"),
            new ICAORange(0x084000, 0x084FFF, "TD", "Chad"),
            new ICAORange(0x088000, 0x088FFF, "TG", "Togo"),
            new ICAORange(0x08A000, 0x08AFFF, "ZM", "Zambia"),
            new ICAORange(0x08C000, 0x08CFFF, "CD", "DR Congo"),
            new ICAORange(0x090000, 0x090FFF, "AO", "Angola"),
            new ICAORange(0x094000, 0x0943FF, "BJ", "Benin"),
            new ICAORange(0x096000, 0x0963FF, "CV", "Cape Verde"),
            new ICAORange(0x098000, 0x0983FF, "DJ", "Djibouti"),
            new ICAORange(0x09A000, 0x09AFFF, "GM", "Gambia"),
            new ICAORange(0x09C000, 0x09CFFF, "BF", "Burkina Faso"),
            new ICAORange(0x09E000, 0x09E3FF, "ST", "Sao Tome & Principe"),
            new ICAORange(0x0A0000, 0x0A7FFF, "DZ", "Algeria"),
        
            // --- Americas ---
            new ICAORange(0x0A8000, 0x0A8FFF, "BS", "Bahamas"),
            new ICAORange(0x0AA000, 0x0AA3FF, "BB", "Barbados"),
            new ICAORange(0x0AB000, 0x0AB3FF, "BZ", "Belize"),
            new ICAORange(0x0AC000, 0x0ACFFF, "CO", "Colombia"),
            new ICAORange(0x0AE000, 0x0AEFFF, "CR", "Costa Rica"),
            new ICAORange(0x0B0000, 0x0B0FFF, "CU", "Cuba"),
            new ICAORange(0x0B2000, 0x0B2FFF, "SV", "El Salvador"),
            new ICAORange(0x0B4000, 0x0B4FFF, "GT", "Guatemala"),
            new ICAORange(0x0B6000, 0x0B6FFF, "GY", "Guyana"),
            new ICAORange(0x0B8000, 0x0B8FFF, "HT", "Haiti"),
            new ICAORange(0x0BA000, 0x0BAFFF, "HN", "Honduras"),
            new ICAORange(0x0BC000, 0x0BC3FF, "VC", "Saint Vincent & the Grenadines"),
            new ICAORange(0x0BE000, 0x0BEFFF, "JM", "Jamaica"),
            new ICAORange(0x0C0000, 0x0C0FFF, "NI", "Nicaragua"),
            new ICAORange(0x0C2000, 0x0C2FFF, "PA", "Panama"),
            new ICAORange(0x0C4000, 0x0C4FFF, "DO", "Dominican Republic"),
            new ICAORange(0x0C6000, 0x0C6FFF, "TT", "Trinidad & Tobago"),
            new ICAORange(0x0C8000, 0x0C8FFF, "SR", "Suriname"),
            new ICAORange(0x0CA000, 0x0CA3FF, "AG", "Antigua & Barbuda"),
            new ICAORange(0x0CC000, 0x0CC3FF, "GD", "Grenada"),
            new ICAORange(0x0D0000, 0x0D7FFF, "MX", "Mexico"),
            new ICAORange(0x0D8000, 0x0DFFFF, "VE", "Venezuela"),
            new ICAORange(0xA00000, 0xAFFFFF, "US", "United States"),
            new ICAORange(0xC00000, 0xC3FFFF, "CA", "Canada"),
            new ICAORange(0xE00000, 0xE3FFFF, "AR", "Argentina"),
            new ICAORange(0xE40000, 0xE7FFFF, "BR", "Brazil"),
            new ICAORange(0xE80000, 0xE80FFF, "CL", "Chile"),
            new ICAORange(0xE84000, 0xE84FFF, "EC", "Ecuador"),
            new ICAORange(0xE88000, 0xE88FFF, "PY", "Paraguay"),
            new ICAORange(0xE8C000, 0xE8CFFF, "PE", "Peru"),
            new ICAORange(0xE90000, 0xE90FFF, "UY", "Uruguay"),
            new ICAORange(0xE94000, 0xE94FFF, "BO", "Bolivia"),
        
            // --- Europe ---
            new ICAORange(0x100000, 0x1FFFFF, "RU", "Russia"),
            new ICAORange(0x300000, 0x33FFFF, "IT", "Italy"),
            new ICAORange(0x340000, 0x37FFFF, "ES", "Spain"),
            new ICAORange(0x380000, 0x3BFFFF, "FR", "France"),
            new ICAORange(0x3C0000, 0x3FFFFF, "DE", "Germany"),
            // UK Territories (must be before the main UK range)
            new ICAORange(0x400000, 0x4001BF, "BM", "Bermuda"),
            new ICAORange(0x4001C0, 0x4001FF, "KY", "Cayman Islands"),
            new ICAORange(0x400300, 0x4003FF, "TC", "Turks & Caicos Islands"),
            new ICAORange(0x424135, 0x4241F2, "KY", "Cayman Islands"),
            new ICAORange(0x424200, 0x4246FF, "BM", "Bermuda"),
            new ICAORange(0x424700, 0x424899, "KY", "Cayman Islands"),
            new ICAORange(0x424B00, 0x424BFF, "IM", "Isle of Man"),
            new ICAORange(0x43BE00, 0x43BEFF, "BM", "Bermuda"),
            new ICAORange(0x43E700, 0x43EAFD, "IM", "Isle of Man"),
            new ICAORange(0x43EAFE, 0x43EEFF, "GG", "Guernsey"),
            new ICAORange(0x400000, 0x43FFFF, "GB", "United Kingdom"), // Main UK Range
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
            new ICAORange(0x498000, 0x49FFFF, "CZ", "Czechia"),
            new ICAORange(0x4A0000, 0x4A7FFF, "RO", "Romania"),
            new ICAORange(0x4A8000, 0x4AFFFF, "SE", "Sweden"),
            new ICAORange(0x4B0000, 0x4B7FFF, "CH", "Switzerland"),
            new ICAORange(0x4B8000, 0x4BFFFF, "TR", "Turkey"),
            new ICAORange(0x4C0000, 0x4C7FFF, "RS", "Serbia"),
            new ICAORange(0x4C8000, 0x4C83FF, "CY", "Cyprus"),
            new ICAORange(0x4CA000, 0x4CAFFF, "IE", "Ireland"),
            new ICAORange(0x4CC000, 0x4CCFFF, "IS", "Iceland"),
            new ICAORange(0x4D0000, 0x4D03FF, "LU", "Luxembourg"),
            new ICAORange(0x4D2000, 0x4D2FFF, "MT", "Malta"),
            new ICAORange(0x4D4000, 0x4D43FF, "MC", "Monaco"),
            new ICAORange(0x500000, 0x5003FF, "SM", "San Marino"),
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
            new ICAORange(0x512000, 0x5123FF, "MK", "Macedonia"),
            new ICAORange(0x513000, 0x5133FF, "BA", "Bosnia & Herzegovina"),
            new ICAORange(0x514000, 0x5143FF, "GE", "Georgia"),
            new ICAORange(0x515000, 0x5153FF, "TJ", "Tajikistan"),
            new ICAORange(0x516000, 0x5163FF, "ME", "Montenegro"),
        
            // --- Asia & Middle East ---
            new ICAORange(0x201000, 0x2013FF, "NA", "Namibia"),
            new ICAORange(0x202000, 0x2023FF, "ER", "Eritrea"),
            new ICAORange(0x600000, 0x6003FF, "AM", "Armenia"),
            new ICAORange(0x600800, 0x600BFF, "AZ", "Azerbaijan"),
            new ICAORange(0x601000, 0x6013FF, "KG", "Kyrgyzstan"),
            new ICAORange(0x601800, 0x601BFF, "TM", "Turkmenistan"),
            new ICAORange(0x680000, 0x6803FF, "BT", "Bhutan"),
            new ICAORange(0x681000, 0x6813FF, "FM", "Micronesia"),
            new ICAORange(0x682000, 0x6823FF, "MN", "Mongolia"),
            new ICAORange(0x683000, 0x6833FF, "KZ", "Kazakhstan"),
            new ICAORange(0x684000, 0x6843FF, "PW", "Palau"),
            new ICAORange(0x700000, 0x700FFF, "AF", "Afghanistan"),
            new ICAORange(0x702000, 0x702FFF, "BD", "Bangladesh"),
            new ICAORange(0x704000, 0x704FFF, "MM", "Myanmar"),
            new ICAORange(0x706000, 0x706FFF, "KW", "Kuwait"),
            new ICAORange(0x708000, 0x708FFF, "LA", "Laos"),
            new ICAORange(0x70A000, 0x70AFFF, "NP", "Nepal"),
            new ICAORange(0x70C000, 0x70C3FF, "OM", "Oman"),
            new ICAORange(0x70E000, 0x70EFFF, "KH", "Cambodia"),
            new ICAORange(0x710000, 0x717FFF, "SA", "Saudi Arabia"),
            new ICAORange(0x718000, 0x71FFFF, "KR", "South Korea"),
            new ICAORange(0x720000, 0x727FFF, "KP", "North Korea"),
            new ICAORange(0x728000, 0x72FFFF, "IQ", "Iraq"),
            new ICAORange(0x730000, 0x737FFF, "IR", "Iran"),
            new ICAORange(0x738000, 0x73FFFF, "IL", "Israel"),
            new ICAORange(0x740000, 0x747FFF, "JO", "Jordan"),
            new ICAORange(0x748000, 0x74FFFF, "LB", "Lebanon"),
            new ICAORange(0x750000, 0x757FFF, "MY", "Malaysia"),
            new ICAORange(0x758000, 0x75FFFF, "PH", "Philippines"),
            new ICAORange(0x760000, 0x767FFF, "PK", "Pakistan"),
            new ICAORange(0x768000, 0x76FFFF, "SG", "Singapore"),
            new ICAORange(0x770000, 0x777FFF, "LK", "Sri Lanka"),
            new ICAORange(0x778000, 0x77FFFF, "SY", "Syria"),
            new ICAORange(0x789000, 0x789FFF, "HK", "Hong Kong"),
            new ICAORange(0x780000, 0x7BFFFF, "CN", "China"),
            new ICAORange(0x800000, 0x83FFFF, "IN", "India"),
            new ICAORange(0x840000, 0x87FFFF, "JP", "Japan"),
            new ICAORange(0x880000, 0x887FFF, "TH", "Thailand"),
            new ICAORange(0x888000, 0x88FFFF, "VN", "Viet Nam"),
            new ICAORange(0x890000, 0x890FFF, "YE", "Yemen"),
            new ICAORange(0x894000, 0x894FFF, "BH", "Bahrain"),
            new ICAORange(0x895000, 0x8953FF, "BN", "Brunei"),
            new ICAORange(0x896000, 0x896FFF, "AE", "United Arab Emirates"),
            new ICAORange(0x897000, 0x8973FF, "SB", "Solomon Islands"),
            new ICAORange(0x898000, 0x898FFF, "PG", "Papua New Guinea"),
            new ICAORange(0x899000, 0x8993FF, "TW", "Taiwan"),
            new ICAORange(0x8A0000, 0x8A7FFF, "ID", "Indonesia"),
        
            // --- Oceania ---
            new ICAORange(0x7C0000, 0x7FFFFF, "AU", "Australia"),
            new ICAORange(0x900000, 0x9003FF, "MH", "Marshall Islands"),
            new ICAORange(0x901000, 0x9013FF, "CK", "Cook Islands"),
            new ICAORange(0x902000, 0x9023FF, "WS", "Samoa"),
            new ICAORange(0xC80000, 0xC87FFF, "NZ", "New Zealand"),
            new ICAORange(0xC88000, 0xC88FFF, "FJ", "Fiji"),
            new ICAORange(0xC8A000, 0xC8A3FF, "NR", "Nauru"),
            new ICAORange(0xC8C000, 0xC8C3FF, "LC", "Saint Lucia"),
            new ICAORange(0xC8D000, 0xC8D3FF, "TU", "Tonga"),
            new ICAORange(0xC8E000, 0xC8E3FF, "KI", "Kiribati"),
            new ICAORange(0xC90000, 0xC903FF, "VU", "Vanuatu")
        };

        private static readonly ICAORange[] MilitaryRanges = new ICAORange[]
        {
            new ICAORange(0xADF7C8, 0xAFFFFF, "US", "United States Military"),
            new ICAORange(0x010070, 0x01008F, "ICAO", "ICAO Reserved (Often Airbus Dev)"),
            new ICAORange(0x0A4000, 0x0A4FFF, "ZA", "South Africa Military"),
            new ICAORange(0x33FF00, 0x33FFFF, "IT", "Italy Military"),
            new ICAORange(0x350000, 0x37FFFF, "DE", "Germany Government/Military"),
            new ICAORange(0x3A8000, 0x3AFFFF, "DE", "Germany Government/Military"),
            new ICAORange(0x3B0000, 0x3BFFFF, "DE", "Germany Military"),
            new ICAORange(0x3EA000, 0x3EBFFF, "FR", "France Government/Military"),
            new ICAORange(0x3F4000, 0x3FBFFF, "FR", "France Military"),
            new ICAORange(0x400000, 0x40003F, "GB", "United Kingdom (Test/Experimental)"),
            new ICAORange(0x43C000, 0x43CFFF, "GB", "United Kingdom (Royal Air Force)"),
            new ICAORange(0x444000, 0x446FFF, "NATO", "NATO"),
            new ICAORange(0x44F000, 0x44FFFF, "NATO", "NATO"),
            new ICAORange(0x457000, 0x457FFF, "NL", "Netherlands Military"),
            new ICAORange(0x45F400, 0x45F4FF, "CH", "Switzerland Military"),
            new ICAORange(0x468000, 0x4683FF, "TR", "Turkey Military"),
            new ICAORange(0x473C00, 0x473C0F, "ES", "Spain Military"),
            new ICAORange(0x478100, 0x4781FF, "ES", "Spain Military"),
            new ICAORange(0x480000, 0x480FFF, "NL", "Netherlands Military"),
            new ICAORange(0x48D800, 0x48D87F, "BE", "Belgium Military"),
            new ICAORange(0x497C00, 0x497CFF, "GR", "Greece Military"),
            new ICAORange(0x498420, 0x49842F, "GR", "Greece Military"),
            new ICAORange(0x4B7000, 0x4B7FFF, "NO", "Norway Military"),
            new ICAORange(0x4B8200, 0x4B82FF, "NO", "Norway Military"),
            new ICAORange(0x506F00, 0x506FFF, "PL", "Poland Military"),
            new ICAORange(0x70C070, 0x70C07F, "CA", "Canada Military"),
            new ICAORange(0x710258, 0x71028F, "US", "United States (Special Use)"),
        //  new ICAORange(0x710380, 0x71039F, "XX", "Undetermined"), // 할당 정보 불명확
            new ICAORange(0x738A00, 0x738AFF, "KR", "Republic of Korea Air Force"),
            new ICAORange(0x7C822E, 0x7C84FF, "AU", "Australia Military"),
            new ICAORange(0x7C8800, 0x7C88FF, "AU", "Australia Military"),
            new ICAORange(0x7C9000, 0x7CBFFF, "AU", "Australia Military"),
            new ICAORange(0x7CF800, 0x7CFAFF, "AU", "Australia Military"),
        //  new ICAORange(0x7D0000, 0x7FFFFF, "XX", "Undetermined"), // 할당 정보 불명확
            new ICAORange(0x800200, 0x8002FF, "RU", "Russia Military"),
            new ICAORange(0xC0CDF9, 0xC3FFFF, "CA", "Canada Military"),
            new ICAORange(0xC87F00, 0xC87FFF, "NZ", "New Zealand Military"),
            new ICAORange(0xE40000, 0xE41FFF, "IN", "India Military")
        };

        private static int ConvertSpriteImage(string icaoaircrafttype, bool isMilitary)
        {
            if (isMilitary)
            {
                switch (icaoaircrafttype)
                {
                    case "H3T":
                    case "H1P":
                    case "H1T":
                    case "H2T":
                        return 72;
                    default:
                        return 76;
                }
            } else
            {
                switch (icaoaircrafttype)
                {
                    case "L1P": return 43;
                    case "LTA": return 1;
                    case "BALL": return 2;
                    case "L2P": return 55;
                    case "LJ40": return 13;
                    case "L2J": return 0;
                    case "GLEX": return 13;
                    case "GLF6": return 13;
                    case "CL35": return 13;
                    case "L6J": return 17;
                    case "L4J": return 10;
                    case "L4T": return 42;
                    case "GDZ": return 41;
                    case "L2T": return 15;
                    case "L3J": return 33;
                    case "FA8X": return 13;
                    case "T2T": return 20;
                    case "UAV": return 28;
                    case "L8J": return 17;
                    case "L3P": return 37;
                    case "L4P": return 32;
                    case "S4P": return 32;
                    case "A2P": return 14;
                    case "L4E": return 77;
                    case "G1P": return 41;
                    case "H1P": return 51;
                    case "H1T": return 46;
                    case "H2T": return 52;
                    case "Z391": return 78;
                    case "H3T": return 74;
                    case "P": return 47;
                    case "H2P": return 51;
                    case "H25B": return 13;
                    default:
                        // 목록에 없는 값이 들어올 경우 기본값 반환
                        return 0;
                }
            }
        }

        public static void Init ()
        {
            Task.Run(() =>
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AircraftDB", "aircraftDatabase_new.csv");

                if (!File.Exists(fullPath))
                    return;

                string data = File.ReadAllText(fullPath);

                List<Dictionary<string, string>> parseData = CsvUtil.Parse(data);
                _aircraftDataTable = CreateAircraftDictionary(parseData);

                EventBus.Publish(EventIds.EvtAircraftDBInitialized, null);
            });
        }

        public static AircraftData GetAircraftInfo(uint addr)
        {
            if (_aircraftDataTable != null && _aircraftDataTable.Count > 0 && _aircraftDataTable.TryGetValue(addr, out var data)) {
                return data;
            }

            (var countryShort, var country) = GetCountry(addr);
            var isMilitary = IsMilitary(addr);

            return new AircraftData { Icao24 = addr, Country = country, CountryShort = countryShort, IsMilitary = isMilitary, AircraftImageNum = isMilitary ? 76 : 0 };
        }

        public static (string, string) GetCountry(uint addr)
        {
            foreach (var range in CountryRanges)
            {
                if (addr >= range.Low && addr <= range.High)
                    return (range.ShortCode, range.LongName);
            }
            return (string.Empty, string.Empty);
        }

        public static bool IsMilitary(uint addr)
        {
            foreach (var range in MilitaryRanges) {
                if (addr >= range.Low && addr <= range.High) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// AircraftData.Icao24가 uint 타입으로 변경된 것을 반영한 리플렉션 변환 메서드입니다.
        /// </summary>
        public static Dictionary<uint, AircraftData> CreateAircraftDictionary(List<Dictionary<string, string>> dataList)
        {
            var aircraftDataTable = new Dictionary<uint, AircraftData>();
            if (dataList == null) return aircraftDataTable;

            var properties = typeof(AircraftData).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                  .Where(p => p.CanWrite).ToList();

            foreach (var dict in dataList)
            {
                var icaoEntry = dict.FirstOrDefault(kvp => kvp.Key.Equals("Icao24", StringComparison.OrdinalIgnoreCase));

                if (icaoEntry.Key == null || string.IsNullOrEmpty(icaoEntry.Value) ||
                    !uint.TryParse(icaoEntry.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint icaoUintValue))
                {
                    continue;
                }

                var aircraft = new AircraftData { Icao24 = icaoUintValue };

                foreach (var prop in properties)
                {
                    if (prop.Name.Equals(nameof(AircraftData.Icao24), StringComparison.OrdinalIgnoreCase)) continue;

                    var dictEntry = dict.FirstOrDefault(kvp => kvp.Key.Equals(prop.Name, StringComparison.OrdinalIgnoreCase));
                    if (dictEntry.Key == null || string.IsNullOrEmpty(dictEntry.Value))
                    {
                        continue;
                    }

                    object convertedValue = null;
                    Type propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    if (propType == typeof(string))
                    {
                        convertedValue = dictEntry.Value;
                    }
                    else if (propType == typeof(int))
                    {
                        if (int.TryParse(dictEntry.Value, out int intVal)) convertedValue = intVal;
                    }
                    else if (propType == typeof(bool))
                    {
                        if (bool.TryParse(dictEntry.Value, out bool boolVal)) convertedValue = boolVal;
                    }
                    else if (propType == typeof(DateTime))
                    {
                        if (DateTime.TryParse(dictEntry.Value, out DateTime dateVal)) convertedValue = dateVal;
                    }

                    if (convertedValue != null)
                    {
                        prop.SetValue(aircraft, convertedValue);
                    }
                }

                (aircraft.CountryShort, aircraft.Country) = GetCountry(aircraft.Icao24);

                aircraft.IsMilitary = IsMilitary(aircraft.Icao24);
                // 72
                aircraft.AircraftImageNum = ConvertSpriteImage(aircraft.IcaoAircraftType, aircraft.IsMilitary);

                aircraftDataTable[icaoUintValue] = aircraft;
            }
            return aircraftDataTable;
        }

        public static IList<AircraftData> GetAllAircraftData()
        {
            if (_aircraftDataTable == null || _aircraftDataTable.Count == 0)
                return new List<AircraftData>();
            return _aircraftDataTable.Values.ToList();
        }
    }
}
