using ADS_B_Display;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Documents;

namespace ADS_B_Display
{
    /// <summary>
    /// SBS_Message 관련 상수 및 메서드를 C#으로 변환한 버전
    /// </summary>
    internal static class SBSMessage
    {
        private const double SMALL_VAL = 0.0001;
        private const double BIG_VAL = 9999999.0;
        private const int MODES_MAX_SBS_SIZE = 256;
        private const uint MODES_NON_ICAO_ADDRESS = 0x800000; // 예시 플래그

        private static bool VALID_POS(Aircraft pos) =>
            Math.Abs(pos.Longitude) >= SMALL_VAL && Math.Abs(pos.Longitude) < 180.0 &&
            Math.Abs(pos.Latitude) >= SMALL_VAL && Math.Abs(pos.Latitude) < 90.0;

        // SBS 필드 인덱스
        private const int SBS_MESSAGE_TYPE = 0;
        private const int SBS_TRANSMISSION_TYPE = 1;
        private const int SBS_SESSION_ID = 2;
        private const int SBS_AIRCRAFT_ID = 3;
        private const int SBS_HEX_INDENT = 4;
        private const int SBS_FLIGHT_ID = 5;
        private const int SBS_DATE_GENERATED = 6;
        private const int SBS_TIME_GENERATED = 7;
        private const int SBS_DATE_LOGGED = 8;
        private const int SBS_TIME_LOGGED = 9;
        private const int SBS_CALLSIGN = 10;
        private const int SBS_ALTITUDE = 11;
        private const int SBS_GROUND_SPEED = 12;
        private const int SBS_TRACK_HEADING = 13;
        private const int SBS_LATITUDE = 14;
        private const int SBS_LONGITUDE = 15;
        private const int SBS_VERTICAL_RATE = 16;
        private const int SBS_SQUAWK = 17;
        private const int SBS_ALERT = 18;
        private const int SBS_EMERGENCY = 19;
        private const int SBS_SBI = 20;
        private const int SBS_IS_ON_GROUND = 21;

        /// <summary>
        /// 16진수 문자 하나를 정수 값(0~15)으로 변환. 실패 시 -1 반환
        /// </summary>
        private static int HexDigitVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return -1;
        }

        /// <summary>
        /// 현재 시각을 "yyyy/MM/dd,HH:mm:ss.fff,yyyy/MM/dd,HH:mm:ss.fff" 형태로 반환
        /// </summary>
        private static string GetSbsTimestamp()
        {
            DateTime now = DateTime.Now;
            string ts = now.ToString("yyyy/MM/dd,HH:mm:ss.fff", CultureInfo.InvariantCulture);
            return $"{ts},{ts}";
        }

        /// <summary>
        /// 24비트 big-endian을 host-order uint로 변환
        /// </summary>
        private static uint AircraftGetAddr(byte a0, byte a1, byte a2)
        {
            return (uint)((a0 << 16) | (a1 << 8) | a2);
        }

        /// <summary>
        /// modeS_message와 Aircraft 객체를 바탕으로 SBS 형식 문자열을 생성
        /// </summary>
        private static bool ModeS_Build_SBS_Message(ModeSMessage mm, Aircraft a, out string sbs)
        {
            // 출력 버퍼를 StringBuilder로 처리
            var sb = new StringBuilder(MODES_MAX_SBS_SIZE);
            int emergency = 0, ground = 0, alert = 0, spi = 0;
            string dateStr = GetSbsTimestamp();

            // 특정 DF 타입에서 상태를 설정
            if (mm.msg_type == 4 || mm.msg_type == 5 || mm.msg_type == 21) {
                if (mm.identity == 7500 || mm.identity == 7600 || mm.identity == 7700)
                    emergency = -1;
                if (mm.flight_status == 1 || mm.flight_status == 3)
                    ground = -1;
                if (mm.flight_status == 2 || mm.flight_status == 3 || mm.flight_status == 4)
                    alert = -1;
                if (mm.flight_status == 4 || mm.flight_status == 5)
                    spi = -1;
            }

            // hex 6자리 ICAO 주소
            string hexIcao = mm.AA[0].ToString("X2") + mm.AA[1].ToString("X2") + mm.AA[2].ToString("X2");

            switch (mm.msg_type) {
                case 0:
                    // DF0: 기본 고도 메시지
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "MSG,5,1,1,{0},1,{1},,{2},,,,,,,,,,,",
                        hexIcao, dateStr, mm.altitude);
                    break;

                case 4:
                    // DF4: 고도 + 상태
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "MSG,5,1,1,{0},1,{1},,{2},,,,,,,{3},{4},{5},{6}",
                        hexIcao, dateStr, mm.altitude, alert, emergency, spi, ground);
                    break;

                case 5:
                    // DF5: 스쿼크(식별) + 상태
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "MSG,6,1,1,{0},1,{1},,,,,,,,,{2},{3},{4},{5},{6}",
                        hexIcao, dateStr, mm.identity, alert, emergency, spi, ground);
                    break;

                case 11:
                    // DF11: 타워 뷰/기본 감시
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "MSG,8,1,1,{0},1,{1},,,,,,,,,,,,,",
                        hexIcao, dateStr);
                    break;

                case 17 when mm.ME_type == 4:
                    // DF17 ME_type 4: 항공기 식별 및 카테고리
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "MSG,1,1,1,{0},1,{1},{2},,,,,,,,,0,0,0,0",
                        hexIcao, dateStr, mm.flight);
                    break;

                case 17 when (mm.ME_type >= 9 && mm.ME_type <= 18):
                    // DF17 ME_type 9~18: 비행 중 위치 메시지
                    if (!a.HaveLatLon || !VALID_POS(a)) {
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "MSG,3,1,1,{0},1,{1},,{2},,,,,,,0,0,0,0",
                            hexIcao, dateStr, mm.altitude);
                    } else {
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "MSG,3,1,1,{0},1,{1},,{2},,,{3:F5},{4:F5},,,0,0,0,0",
                            hexIcao, dateStr, mm.altitude, a.Latitude, a.Longitude);
                    }
                    break;

                case 17 when (mm.ME_type == 19 && mm.ME_subtype == 1):
                    // DF17 ME_type 19 subtype 1: 속도/고도
                    int vr = ((mm.vert_rate_sign == 0 ? 1 : -1) * 64 * (mm.vert_rate - 1));
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "MSG,4,1,1,{0},1,{1},,,{2},{3},,,{4},,,0,0,0,0",
                        hexIcao, dateStr, (int)a.Speed, (int)a.Heading, vr);
                    break;

                case 21:
                    // DF21: Comm-B identity + 상태
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "MSG,6,1,1,{0},1,{1},,,,,,,,,{2},{3},{4},{5},{6}",
                        hexIcao, dateStr, mm.identity, alert, emergency, spi, ground);
                    break;

                default:
                    sbs = string.Empty;
                    return false;
            }

            sbs = sb.ToString();
            return true;
        }

        /// <summary>
        /// 쉼표 구분자로 분할(원래 C strsep 대체)
        /// </summary>
        internal static string[] SplitSbsFields(string msg)
        {
            // 최대 22개 필드
            var fields = new string[22];
            int index = 0;
            int start = 0;
            for (int i = 0; i < msg.Length && index < 22; i++) {
                if (msg[i] == ',') {
                    fields[index++] = msg.Substring(start, i - start);
                    start = i + 1;
                }
            }
            // 마지막 필드
            if (index < 22) {
                fields[index++] = msg.Substring(start);
            }
            return fields;
        }

        internal static bool VerifySbsAndIcao(string[] SBS_Fields, out uint addr)
        {
            addr = 0;
            if (SBS_Fields.Length < 22) return false;

            // MSG인지 확인
            if (!string.Equals(SBS_Fields[SBS_MESSAGE_TYPE], "MSG", StringComparison.OrdinalIgnoreCase))
                return false;

            // ICAO 필드 검사 및 6자리 맞춤
            string hexField = SBS_Fields[SBS_HEX_INDENT];
            if (string.IsNullOrEmpty(hexField) || hexField.Length < 6 || hexField.Length > 7) {
                if (string.IsNullOrEmpty(hexField) || hexField.Length > 7)
                    return false;

                int pad = 6 - hexField.Length;
                hexField = new string('0', pad) + hexField;
                SBS_Fields[SBS_HEX_INDENT] = hexField;
            }

            bool nonIcao = false;
            if (hexField[0] == '~') {
                nonIcao = true;
                hexField = hexField.Substring(1);
            }

            // 6자리 16진수 → uint addr
            if (hexField.Length != 6) return false;
            for (int i = 0; i < 6; i += 2) {
                int high = HexDigitVal(hexField[i]);
                int low = HexDigitVal(hexField[i + 1]);
                if (high < 0 || low < 0) return false;
                addr |= (uint)((high << 4 | low) << (8 * (2 - i / 2)));
            }
            if (nonIcao) addr |= MODES_NON_ICAO_ADDRESS;

            return true;
        }

        /// <summary>
        /// SBS 형식 문자열을 디코딩하여 해시 테이블의 Aircraft 객체를 업데이트
        /// </summary>
        internal static bool SBS_Message_Decode(string[] SBS_Fields, ref Aircraft aircraft)
        {
            // 전역 해시 테이블(ght_get)에서 Aircraft 검색/생성
            long currentTime = TimeFunctions.GetCurrentTimeInMsec();
            aircraft.LastSeen = currentTime;
            aircraft.NumMessagesSBS++;

            // Callsign (필드 10)
            string callsign = SBS_Fields[SBS_CALLSIGN];
            if (!string.IsNullOrEmpty(callsign)) {
                callsign = callsign.ToUpperInvariant();
                if (callsign.Length > 8) callsign = callsign.Substring(0, 8);
                callsign = callsign.PadRight(8, ' ');
                bool valid = true;
                foreach (char c in callsign) {
                    if (!char.IsLetterOrDigit(c) && c != ' ') {
                        valid = false;
                        break;
                    }
                }
                if (valid) {
                    aircraft.FlightNum = callsign;
                    aircraft.HaveFlightNum = true;
                }
            }

            // Altitude (필드 11)
            string altStr = SBS_Fields[SBS_ALTITUDE];
            if (!string.IsNullOrEmpty(altStr) && double.TryParse(altStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double altVal)) {
                if (!double.IsInfinity(altVal)) {
                    if (altVal <= 1)
                    {
                        aircraft.HaveAltitude = false;
                    }
                    else
                    {
                        aircraft.HaveAltitude = true;
                    }
                    aircraft.Altitude = altVal;
                }
            }

            // Ground Speed (필드 12)
            string spdStr = SBS_Fields[SBS_GROUND_SPEED];
            if (!string.IsNullOrEmpty(spdStr) && double.TryParse(spdStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double spdVal)) {
                if (!double.IsInfinity(spdVal)) {
                    aircraft.Speed = spdVal;
                }
            }

            // Track / Heading (필드 13)
            string hdgStr = SBS_Fields[SBS_TRACK_HEADING];
            if (!string.IsNullOrEmpty(hdgStr) && double.TryParse(hdgStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double hdgVal)) {
                if (!double.IsInfinity(hdgVal)) {
                    aircraft.Heading = hdgVal;
                    aircraft.HaveSpeedAndHeading = true;
                }
            }

            // Latitude & Longitude (필드 14, 15)
            string latStr = SBS_Fields[SBS_LATITUDE];
            string lonStr = SBS_Fields[SBS_LONGITUDE];
            if (!string.IsNullOrEmpty(latStr) && !string.IsNullOrEmpty(lonStr) &&
                double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double latVal) &&
                double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lonVal)) {
                if (!double.IsInfinity(latVal) && !double.IsInfinity(lonVal) &&
                    Math.Abs(latVal) < 90.0 && Math.Abs(lonVal) < 180.0) {
                    aircraft.Latitude = latVal;
                    aircraft.Longitude = lonVal;
                    aircraft.HaveLatLon = true;
                }
            }

            // Vertical Rate (필드 16)
            string vrStr = SBS_Fields[SBS_VERTICAL_RATE];
            if (!string.IsNullOrEmpty(vrStr) && double.TryParse(vrStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double vrVal)) {
                if (!double.IsInfinity(vrVal)) {
                    aircraft.VerticalRate = vrVal;
                }
            }

            return true;
        }
    }
}
