using ADS_B_Display;
using ADS_B_Display.Models;
using ADS_B_Display.Models.Parser;
using System;
using System.Runtime.InteropServices;

namespace ADS_B_Display.Models.Parser
{
    /// <summary>
    /// 상수 및 매크로 정의
    /// </summary>
    public static class DecodeRawAdsBConstants
    {
        public const double TWO_PI = 2 * Math.PI;

        public const int MODES_PREAMBLE_US = 8;            // microseconds
        public const int MODES_LONG_MSG_BITS = 112;
        public const int MODES_SHORT_MSG_BITS = 56;
        public const int MODES_FULL_LEN = MODES_PREAMBLE_US + MODES_LONG_MSG_BITS;
        public const int MODES_LONG_MSG_BYTES = MODES_LONG_MSG_BITS / 8;
        public const int MODES_SHORT_MSG_BYTES = MODES_SHORT_MSG_BITS / 8;
        public const int MODES_MAX_SBS_SIZE = 256;

        public const int MODES_ICAO_CACHE_LEN = 1024;      // Power of two required.
        public const int MODES_ICAO_CACHE_TTL = 60;        // Time to live of cached addresses (sec).

        public const bool error_correct_1 = true;  // Fix 1 bit errors (default: true).
        public const bool error_correct_2 = true;  // Fix 2 bit errors (default: false).

        /// <summary>
        /// The `readsb` program will send 5 heart-beats like this in RAW mode.
        /// </summary>
        public const string MODES_RAW_HEART_BEAT = "*0000;\n*0000;\n*0000;\n*0000;\n*0000;\n";

        public static string UnitName(metric_unit_t unit)
        {
            return unit == metric_unit_t.MODES_UNIT_METERS ? "meters" : "feet";
        }
    }

    /// <summary>
    /// 측정 단위
    /// </summary>
    public enum metric_unit_t
    {
        MODES_UNIT_FEET = 1,
        MODES_UNIT_METERS = 2
    }

    /// <summary>
    /// DecodeRawADS_B 헤더에서 정의된 modeS_message 구조를 C# 클래스 형태로 변환
    /// </summary>
    public class ModeSMessage
    {
        // Binary message (up to 14 bytes).
        public byte[] msg { get; } = new byte[DecodeRawAdsBConstants.MODES_LONG_MSG_BYTES];

        // Number of bits in message.
        public int msg_bits { get; set; }

        // Downlink format #.
        public int msg_type { get; set; }

        // True if CRC was valid.
        public bool CRC_ok { get; set; }

        // Message CRC.
        public uint CRC { get; set; }

        // RSSI, in the range [0..1], as a fraction of full-scale power.
        public double sig_level { get; set; }

        // Bit corrected. -1 if no bit corrected.
        public int error_bit { get; set; }

        // ICAO Address bytes 1, 2 and 3 (big-endian).
        public byte[] AA { get; } = new byte[3];

        // True if phase correction was applied.
        public bool phase_corrected { get; set; }

        // DF11
        public int ca { get; set; }                     // Responder capabilities.

        // DF 17
        public int ME_type { get; set; }                // Extended squitter message type.
        public int ME_subtype { get; set; }             // Extended squitter message subtype.
        public int heading { get; set; }                // Horizontal angle of flight.
        public bool heading_is_valid { get; set; }      // We got a valid `heading`.
        public int aircraft_type { get; set; }          // Aircraft identification. "Type A..D".
        public int odd_flag { get; set; }               // 1 = Odd, 0 = Even CPR message.
        public int UTC_flag { get; set; }               // UTC synchronized?
        public int raw_latitude { get; set; }           // Non decoded latitude.
        public int raw_longitude { get; set; }          // Non decoded longitude.
        public string flight { get; set; } = new string('\0', 8); // 8 chars flight number
        public int EW_dir { get; set; }                 // 0 = East, 1 = West.
        public int EW_velocity { get; set; }            // E/W velocity.
        public int NS_dir { get; set; }                 // 0 = North, 1 = South.
        public int NS_velocity { get; set; }            // N/S velocity.
        public int vert_rate_source { get; set; }       // Vertical rate source.
        public int vert_rate_sign { get; set; }         // Vertical rate sign.
        public int vert_rate { get; set; }              // Vertical rate.
        public int velocity { get; set; }               // Computed from EW and NS velocity.

        // DF4, DF5, DF20, DF21
        public int flight_status { get; set; }          // Flight status for DF4, 5, 20 and 21.
        public int DR_status { get; set; }              // Request extraction of downlink request.
        public int UM_status { get; set; }              // Request extraction of downlink request.
        public int identity { get; set; }               // 13 bits identity (Squawk).

        // Fields used by multiple message types.
        public int altitude { get; set; }
        public metric_unit_t unit;
    }

    /// <summary>
    /// 디코딩 결과 상태
    /// </summary>
    public enum TDecodeStatus
    {
        HaveMsg = 0,
        MsgHeartBeat = 1,
        CRCError = 2,
        BadMessageHighLow = 3,
        BadMessageTooLong = 4,
        BadMessageFormat1 = 5,
        BadMessageFormat2 = 6,
        BadMessageEmpty1 = 7,
        BadMessageEmpty2 = 8
    }

    public class RawParser : IParser
    {
        // ICAO 캐시 (모든 인스턴스가 공유)
        private uint[] ICAO_cache = new uint[2 * DecodeRawAdsBConstants.MODES_ICAO_CACHE_LEN];

        /// <summary>
        /// hex 문자(0~F) 하나를 정수 값으로 변환. 실패 시 -1 반환
        /// </summary>
        private int HexDigitVal(char c)
        {
            c = char.ToLowerInvariant(c);
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            return -1;
        }

        /// <summary>
        /// 24비트 big-endian (network order) 값을 host order (uint)로 변환
        /// </summary>
        private uint AircraftGetAddr(byte a0, byte a1, byte a2)
        {
            return ((uint)a0 << 16) | ((uint)a1 << 8) | a2;
        }

        /// <summary>
        /// ICAO 주소를 해시하여 캐시 인덱스 생성 (길이는 2^n 여야 함)
        /// </summary>
        private uint ICAO_cache_hash_address(uint a)
        {
            // 세 번의 라운드를 통해 비트들이 고르게 섞이도록 함
            a = ((a >> 16) ^ a) * 0x45D9F3B;
            a = ((a >> 16) ^ a) * 0x45D9F3B;
            a = ((a >> 16) ^ a);
            return a & (uint)(DecodeRawAdsBConstants.MODES_ICAO_CACHE_LEN - 1);
        }

        /// <summary>
        /// 지정된 ICAO 주소를 캐시에 추가(시간 정보 포함)
        /// </summary>
        private void ICAO_cache_add_address(uint addr)
        {
            uint h = ICAO_cache_hash_address(addr);
            ICAO_cache[h * 2] = addr;
            ICAO_cache[h * 2 + 1] = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// 지정된 ICAO 주소가 최근에 캐시에 저장되어 있는지 확인
        /// </summary>
        private bool ICAO_address_recently_seen(uint addr)
        {
            uint h_idx = ICAO_cache_hash_address(addr);
            uint storedAddr = ICAO_cache[h_idx * 2];
            uint seenTime = ICAO_cache[h_idx * 2 + 1];
            return (storedAddr != 0 && storedAddr == addr &&
                    ((DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seenTime) <= DecodeRawAdsBConstants.MODES_ICAO_CACHE_TTL));
        }

        /// <summary>
        /// 메시지 타입(DF)에 따라 비트 길이를 반환
        /// </summary>
        private int ModeS_message_len_by_type(int type)
        {
            if (type == 16 || type == 17 || type == 19 || type == 20 || type == 21)
                return DecodeRawAdsBConstants.MODES_LONG_MSG_BITS;
            return DecodeRawAdsBConstants.MODES_SHORT_MSG_BITS;
        }

        /// <summary>
        /// 주어진 메시지 버퍼의 CRC를 계산 (메시지 맨 끝 3바이트가 CRC)
        /// </summary>
        private uint CRC_get(byte[] msg, int bits)
        {
            int bytes = bits / 8;
            return ((uint)msg[bytes - 3] << 16) |
                   ((uint)msg[bytes - 2] << 8) |
                    (uint)msg[bytes - 1];
        }

        /// <summary>
        /// CRC 체크 계산 (MODES_LONG_MSG_BITS/SHORT)
        /// </summary>
        private uint CRC_check(byte[] msg, int bits)
        {
            uint crc = 0;
            int offset = 0;
            if (bits != DecodeRawAdsBConstants.MODES_LONG_MSG_BITS)
                offset = DecodeRawAdsBConstants.MODES_LONG_MSG_BITS - DecodeRawAdsBConstants.MODES_SHORT_MSG_BITS;

            for (int j = 0; j < bits; j++) {
                int b = j / 8;
                int bit = j % 8;
                int mask = 1 << (7 - bit);
                if ((msg[b] & mask) != 0) {
                    crc ^= ChecksumTable[j + offset];
                }
            }
            return crc;
        }

        /// <summary>
        /// 단일 비트 오류 교정 시도
        /// </summary>
        private int FixSingleBitErrors(byte[] msg, int bits)
        {
            int bytes = bits / 8;
            byte[] aux = new byte[DecodeRawAdsBConstants.MODES_LONG_MSG_BYTES];
            for (int i = 0; i < bits; i++) {
                int b = i / 8;
                int mask = 1 << (7 - (i % 8));
                Array.Copy(msg, aux, bytes);
                aux[b] ^= (byte)mask;

                uint crc1 = CRC_get(aux, bits);
                uint crc2 = CRC_check(aux, bits);
                if (crc1 == crc2) {
                    Array.Copy(aux, msg, bytes);
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 두 비트 오류 교정 시도 (매우 느림)
        /// </summary>
        private int FixTwoBitsErrors(byte[] msg, int bits)
        {
            int bytes = bits / 8;
            byte[] aux = new byte[DecodeRawAdsBConstants.MODES_LONG_MSG_BYTES];
            for (int j = 0; j < bits; j++) {
                int b1 = j / 8;
                int mask1 = 1 << (7 - (j % 8));
                for (int i = j + 1; i < bits; i++) {
                    int b2 = i / 8;
                    int mask2 = 1 << (7 - (i % 8));
                    Array.Copy(msg, aux, bytes);
                    aux[b1] ^= (byte)mask1;
                    aux[b2] ^= (byte)mask2;

                    uint crc1 = CRC_get(aux, bits);
                    uint crc2 = CRC_check(aux, bits);
                    if (crc1 == crc2) {
                        Array.Copy(aux, msg, bytes);
                        return j | (i << 8);
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// AP 필드가 있는 메시지에서 brute-force로 ICAO 주소 복원 시도
        /// </summary>
        private bool BruteForceAP(byte[] msg, ModeSMessage mm)
        {
            int msg_type = mm.msg_type;
            int msg_bits = mm.msg_bits;

            // DF 0,4,5,16,20,21,24 제외
            if (msg_type == 0 || msg_type == 4 || msg_type == 5 ||
                msg_type == 16 || msg_type == 20 || msg_type == 21 ||
                msg_type == 24) {
                int lastByteIdx = (msg_bits / 8) - 1;
                byte[] aux = new byte[DecodeRawAdsBConstants.MODES_LONG_MSG_BYTES];
                Array.Copy(msg, aux, msg_bits / 8);

                uint computedCrc = CRC_check(aux, msg_bits);
                // CRC xor AP 필드를 통해 주소 복원 시도
                aux[lastByteIdx] ^= (byte)(computedCrc & 0xFF);
                aux[lastByteIdx - 1] ^= (byte)((computedCrc >> 8) & 0xFF);
                aux[lastByteIdx - 2] ^= (byte)((computedCrc >> 16) & 0xFF);

                uint addr = AircraftGetAddr(aux[lastByteIdx - 2], aux[lastByteIdx - 1], aux[lastByteIdx]);
                if (ICAO_address_recently_seen(addr)) {
                    mm.AA[0] = aux[lastByteIdx - 2];
                    mm.AA[1] = aux[lastByteIdx - 1];
                    mm.AA[2] = aux[lastByteIdx];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 13비트 AC 고도 필드 디코딩 (DF0, DF4, DF16, DF20 등)
        /// </summary>
        private int DecodeAC13Field(byte[] msg, out metric_unit_t unit)
        {
            bool m_bit = (msg[3] & (1 << 6)) != 0;
            bool q_bit = (msg[3] & (1 << 4)) != 0;
            unit = metric_unit_t.MODES_UNIT_FEET;
            if (!m_bit) {
                if (q_bit) {
                    int n = ((msg[2] & 0x1F) << 6) |
                            ((msg[3] & 0x80) >> 2) |
                            ((msg[3] & 0x20) >> 1) |
                            (msg[3] & 0x0F);
                    int alt = 25 * n - 1000;
                    return alt < 0 ? 0 : alt;
                } else {
                    // TODO: Q=0, M=0 케이스 구현
                }
            } else {
                unit = metric_unit_t.MODES_UNIT_METERS;
                // TODO: Meter unit 케이스 구현
            }
            return 0;
        }

        /// <summary>
        /// 12비트 AC 고도 필드 디코딩 (DF17 등)
        /// </summary>
        private int DecodeAC12Field(byte[] msg, out metric_unit_t unit)
        {
            bool q_bit = (msg[5] & 1) != 0;
            unit = metric_unit_t.MODES_UNIT_FEET;
            if (q_bit) {
                int n = ((msg[5] >> 1) << 4) | ((msg[6] & 0xF0) >> 4);
                int alt = 25 * n - 1000;
                return alt < 0 ? 0 : alt;
            }
            return 0;
        }

        /// <summary>
        /// 주어진 버퍼를 modeS_message 구조로 디코딩
        /// </summary>
        private bool DecodeModeSMessage(ModeSMessage mm, byte[] rawMsg)
        {
            // 로컬 복사본
            Array.Clear(mm.msg, 0, mm.msg.Length);
            Array.Copy(rawMsg, mm.msg, rawMsg.Length);

            // Downlink Format (DF)
            mm.msg_type = mm.msg[0] >> 3;
            mm.msg_bits = ModeS_message_len_by_type(mm.msg_type);
            mm.CRC = CRC_get(mm.msg, mm.msg_bits);
            uint computedCRC = CRC_check(mm.msg, mm.msg_bits);

            mm.error_bit = -1;
            mm.CRC_ok = (mm.CRC == computedCRC);

            if (!mm.CRC_ok && DecodeRawAdsBConstants.error_correct_1 &&
                (mm.msg_type == 11 || mm.msg_type == 17)) {
                mm.error_bit = FixSingleBitErrors(mm.msg, mm.msg_bits);
                if (mm.error_bit != -1) {
                    mm.CRC = CRC_check(mm.msg, mm.msg_bits);
                    mm.CRC_ok = true;
                } else if (DecodeRawAdsBConstants.error_correct_2 &&
                           mm.msg_type == 17) {
                    mm.error_bit = FixTwoBitsErrors(mm.msg, mm.msg_bits);
                    if (mm.error_bit != -1) {
                        mm.CRC = CRC_check(mm.msg, mm.msg_bits);
                        mm.CRC_ok = true;
                    }
                }
            }

            // Responder capabilities
            mm.ca = mm.msg[0] & 0x07;

            // ICAO address
            mm.AA[0] = mm.msg[1];
            mm.AA[1] = mm.msg[2];
            mm.AA[2] = mm.msg[3];

            // Extended squitter message fields (DF17 기준)
            mm.ME_type = mm.msg[4] >> 3;
            mm.ME_subtype = mm.msg[4] & 0x07;

            // DF4,5,20,21: flight status / DR / UM / identity 계산
            mm.flight_status = mm.msg[0] & 0x07;
            mm.DR_status = (mm.msg[1] >> 3) & 0x1F;
            mm.UM_status = ((mm.msg[1] & 0x07) << 3) | (mm.msg[2] >> 5);
            {
                int a = ((mm.msg[3] & 0x80) >> 5) |
                        ((mm.msg[2] & 0x02) >> 0) |
                        ((mm.msg[2] & 0x08) >> 3);
                int b = ((mm.msg[3] & 0x02) << 1) |
                        ((mm.msg[3] & 0x08) >> 2) |
                        ((mm.msg[3] & 0x20) >> 5);
                int c = ((mm.msg[2] & 0x01) << 2) |
                        ((mm.msg[2] & 0x04) >> 1) |
                        ((mm.msg[2] & 0x10) >> 4);
                int d = ((mm.msg[3] & 0x01) << 2) |
                        ((mm.msg[3] & 0x04) >> 1) |
                        ((mm.msg[3] & 0x10) >> 4);
                mm.identity = a * 1000 + b * 100 + c * 10 + d;
            }

            // DF11 & DF17: AP 필드 복원 시도
            if (mm.msg_type != 11 && mm.msg_type != 17) {
                if (BruteForceAP(mm.msg, mm))
                    mm.CRC_ok = true;
                else
                    mm.CRC_ok = false;
            } else {
                if (mm.CRC_ok && mm.error_bit == -1) {
                    uint addr = AircraftGetAddr(mm.AA[0], mm.AA[1], mm.AA[2]);
                    ICAO_cache_add_address(addr);
                }
            }

            // DF0, DF4, DF16, DF20: 13비트 고도 디코딩
            if (mm.msg_type == 0 || mm.msg_type == 4 || mm.msg_type == 16 || mm.msg_type == 20) {
                mm.altitude = DecodeAC13Field(mm.msg, out mm.unit);
            }

            // DF17: Extended squitter message 처리
            if (mm.msg_type == 17) {
                if (mm.ME_type >= 1 && mm.ME_type <= 4) {
                    // Aircraft Identification and Category
                    mm.aircraft_type = mm.ME_type - 1;
                    const string AIS_charset = "?ABCDEFGHIJKLMNOPQRSTUVWXYZ????? ???????????????0123456789??????";

                    char[] flightChars = new char[8];
                    flightChars[0] = AIS_charset[mm.msg[5] >> 2];
                    flightChars[1] = AIS_charset[((mm.msg[5] & 0x03) << 4) | (mm.msg[6] >> 4)];
                    flightChars[2] = AIS_charset[((mm.msg[6] & 0x0F) << 2) | (mm.msg[7] >> 6)];
                    flightChars[3] = AIS_charset[mm.msg[7] & 0x3F];
                    flightChars[4] = AIS_charset[mm.msg[8] >> 2];
                    flightChars[5] = AIS_charset[((mm.msg[8] & 0x03) << 4) | (mm.msg[9] >> 4)];
                    flightChars[6] = AIS_charset[((mm.msg[9] & 0x0F) << 2) | (mm.msg[10] >> 6)];
                    flightChars[7] = AIS_charset[mm.msg[10] & 0x3F];
                    mm.flight = new string(flightChars).TrimEnd();
                } else if (mm.ME_type >= 9 && mm.ME_type <= 18) {
                    // Airborne Position Message
                    mm.odd_flag = (mm.msg[6] & (1 << 2)) != 0 ? 1 : 0;
                    mm.UTC_flag = (mm.msg[6] & (1 << 3)) != 0 ? 1 : 0;
                    mm.altitude = DecodeAC12Field(mm.msg, out mm.unit);
                    mm.raw_latitude = (((mm.msg[6] & 0x03) << 15) | (mm.msg[7] << 7) | (mm.msg[8] >> 1));
                    mm.raw_longitude = (((mm.msg[8] & 0x01) << 16) | (mm.msg[9] << 8) | mm.msg[10]);
                } else if (mm.ME_type == 19 && mm.ME_subtype >= 1 && mm.ME_subtype <= 4) {
                    // Airborne Velocity Message
                    if (mm.ME_subtype == 1 || mm.ME_subtype == 2) {
                        mm.EW_dir = (mm.msg[5] & 0x04) >> 2;
                        mm.EW_velocity = ((mm.msg[5] & 0x03) << 8) | mm.msg[6];
                        mm.NS_dir = (mm.msg[7] & 0x80) >> 7;
                        mm.NS_velocity = ((mm.msg[7] & 0x7F) << 3) | ((mm.msg[8] & 0xE0) >> 5);
                        mm.vert_rate_source = (mm.msg[8] & 0x10) >> 4;
                        mm.vert_rate_sign = (mm.msg[8] & 0x08) >> 3;
                        mm.vert_rate = ((mm.msg[8] & 0x07) << 6) | ((mm.msg[9] & 0xFC) >> 2);

                        mm.velocity = (int)Math.Round(MathExt.Hypot(mm.NS_velocity, mm.EW_velocity));
                        if (mm.velocity != 0) {
                            int ewV = mm.EW_velocity * (mm.EW_dir == 1 ? -1 : 1);
                            int nsV = mm.NS_velocity * (mm.NS_dir == 1 ? -1 : 1);
                            double headingRad = Math.Atan2(ewV, nsV);
                            int headingDeg = (int)(headingRad * 360.0 / DecodeRawAdsBConstants.TWO_PI);
                            if (headingDeg < 0) headingDeg += 360;
                            mm.heading = headingDeg;
                            mm.heading_is_valid = true;
                        } else {
                            mm.heading = 0;
                        }
                    } else if (mm.ME_subtype == 3 || mm.ME_subtype == 4) {
                        mm.heading_is_valid = (mm.msg[5] & (1 << 2)) != 0;
                        mm.heading = (int)((360.0 / 128.0) * (((mm.msg[5] & 0x03) << 5) | (mm.msg[6] >> 3)));
                    }
                }
                // DF19 subtype 5~8: Surface position (생략)
            }

            mm.phase_corrected = false;
            return mm.CRC_ok;
        }

        /// <summary>
        /// RAW SBS-1 메시지(AnsiString) 입력 받아 modeS_message로 디코딩
        /// </summary>
        /// 
        public uint Parse(string msgLine, long time)
        {
            ModeSMessage modeSMessage = new ModeSMessage();
            var status = Decode_RAW_message(msgLine, ref modeSMessage);

            if (status != TDecodeStatus.HaveMsg)
            {
                return 0; // 유효하지 않은 Raw 메시지
            }
            else if (status == TDecodeStatus.MsgHeartBeat)
            {
                return 0;
            }

            uint addr = (uint)((modeSMessage.AA[0] << 16) | (modeSMessage.AA[1] << 8) | modeSMessage.AA[2]);
            var aircraft = AircraftManager.GetOrAdd(addr); // Aircraft 객체를 가져오거나 생성
            RawToAircraft(modeSMessage, ref aircraft, time);

            return addr; // Raw 메시지 처리는 아직 구현되지 않음
        }

        public TDecodeStatus Decode_RAW_message(string MsgIn, ref ModeSMessage mm)
        {
            // 1) 입력 문자열을 바이트 배열로 복사하고 '\n' 추가
            byte[] raw = System.Text.Encoding.ASCII.GetBytes(MsgIn + "\n");
            int msg_len = raw.Length;

            if (msg_len == 0)
                return TDecodeStatus.BadMessageEmpty1;

            // 2) '\n' 위치 찾기
            int newlineIdx = Array.IndexOf(raw, (byte)'\n');
            if (newlineIdx < 0)
                return TDecodeStatus.BadMessageFormat1;

            // 3) '\n'을 '\0'으로 바꿔서 문자열 끝 표시
            raw[newlineIdx] = 0;
            int len = newlineIdx;

            // 4) CRLF 처리
            if (len >= 2 && raw[len - 1] == '\r') {
                raw[len - 1] = 0;
                len--;
            }

            // 5) 원본이 하트비트인지 검사
            string candidate = System.Text.Encoding.ASCII.GetString(raw, 0, len + 1);
            if (candidate == DecodeRawAdsBConstants.MODES_RAW_HEART_BEAT)
                return TDecodeStatus.MsgHeartBeat;

            // 6) 양쪽 공백 제거
            while (len > 0 && char.IsWhiteSpace((char)raw[len - 1])) {
                raw[len - 1] = 0;
                len--;
            }
            int start = 0;
            while (start < len && char.IsWhiteSpace((char)raw[start]))
                start++;

            if (len - start < 2)
                return TDecodeStatus.BadMessageEmpty2;

            // 7) 메시지 형식 검사: 반드시 '*'로 시작, ';' 포함
            if (raw[start] != (byte)'*' || Array.IndexOf(raw, (byte)';', start) < 0)
                return TDecodeStatus.BadMessageFormat2;

            // 8) '*'와 ';' 건너뛰기
            int hexStart = start + 1;
            int hexLen = len - 2; // '*'와 ';' 제외
            if (hexLen > 2 * DecodeRawAdsBConstants.MODES_LONG_MSG_BYTES)
                return TDecodeStatus.BadMessageTooLong;

            // 9) 2글자씩 짝지어 바이너리 메시지 변환
            byte[] bin_msg = new byte[DecodeRawAdsBConstants.MODES_LONG_MSG_BYTES];
            for (int j = 0; j < hexLen; j += 2) {
                int high = HexDigitVal((char)raw[hexStart + j]);
                int low = HexDigitVal((char)raw[hexStart + j + 1]);
                if (high < 0 || low < 0)
                    return TDecodeStatus.BadMessageHighLow;
                bin_msg[j / 2] = (byte)((high << 4) | low);
            }

            // 10) bin_msg를 decode_modeS_message에 전달
            bool crcOk = DecodeModeSMessage(mm, bin_msg);
            return crcOk ? TDecodeStatus.HaveMsg : TDecodeStatus.CRCError;
        }

        // CRC 테이블 (MODES_LONG_MSG_BITS 요소)
        private readonly uint[] ChecksumTable = new uint[]
        {
            0x3935EA, 0x1C9AF5, 0xF1B77E, 0x78DBBF, 0xC397DB, 0x9E31E9, 0xB0E2F0, 0x587178,
            0x2C38BC, 0x161C5E, 0x0B0E2F, 0xFA7D13, 0x82C48D, 0xBE9842, 0x5F4C21, 0xD05C14,
            0x682E0A, 0x341705, 0xE5F186, 0x72F8C3, 0xC68665, 0x9CB936, 0x4E5C9B, 0xD8D449,
            0x939020, 0x49C810, 0x24E408, 0x127204, 0x093902, 0x049C81, 0xFDB444, 0x7EDA22,
            0x3F6D11, 0xE04C8C, 0x702646, 0x381323, 0xE3F395, 0x8E03CE, 0x4701E7, 0xDC7AF7,
            0x91C77F, 0xB719BB, 0xA476D9, 0xADC168, 0x56E0B4, 0x2B705A, 0x15B82D, 0xF52612,
            0x7A9309, 0xC2B380, 0x6159C0, 0x30ACE0, 0x185670, 0x0C2B38, 0x06159C, 0x030ACE,
            0x018567, 0xFF38B7, 0x80665F, 0xBFC92B, 0xA01E91, 0xAFF54C, 0x57FAA6, 0x2BFD53,
            0xEA04AD, 0x8AF852, 0x457C29, 0xDD4410, 0x6EA208, 0x375104, 0x1BA882, 0x0DD441,
            0xF91024, 0x7C8812, 0x3E4409, 0xE0D800, 0x706C00, 0x383600, 0x1C1B00, 0x0E0D80,
            0x0706C0, 0x038360, 0x01C1B0, 0x00E0D8, 0x00706C, 0x003836, 0x001C1B, 0xFFF409,
            // 마지막 56개는 모두 0
            0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000,
            0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000,
            0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000,
            0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000,
            0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000,
            0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000,
            0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000, 0x000000
        };

        public void RawToAircraft(ModeSMessage mm, ref Aircraft a, long time)
        {
            RawToAircraft(mm, ref a, time);
        }

        class AircraftDecoder
        {
            private const double SMALL_VAL = 0.0001;
            private const int CPR_BITS = 131072; // 2^17
            private const uint MODES_NON_ICAO_ADDRESS = 1 << 24;

            private int cprNLFunction(double lat)
            {
                lat = Math.Abs(lat);
                if (lat < 10.47047130) return 59;
                if (lat < 14.82817437) return 58;
                if (lat < 18.18626357) return 57;
                if (lat < 21.02939493) return 56;
                if (lat < 23.54504487) return 55;
                if (lat < 25.82924707) return 54;
                if (lat < 27.93898710) return 53;
                if (lat < 29.91135686) return 52;
                if (lat < 31.77209708) return 51;
                if (lat < 33.53993436) return 50;
                if (lat < 35.22899598) return 49;
                if (lat < 36.85025108) return 48;
                if (lat < 38.41241892) return 47;
                if (lat < 39.92256684) return 46;
                if (lat < 41.38651832) return 45;
                if (lat < 42.80914012) return 44;
                if (lat < 44.19454951) return 43;
                if (lat < 45.54626723) return 42;
                if (lat < 46.86733252) return 41;
                if (lat < 48.16039128) return 40;
                if (lat < 49.42776439) return 39;
                if (lat < 50.67150166) return 38;
                if (lat < 51.89342469) return 37;
                if (lat < 53.09516153) return 36;
                if (lat < 54.27817472) return 35;
                if (lat < 55.44378444) return 34;
                if (lat < 56.59318756) return 33;
                if (lat < 57.72747354) return 32;
                if (lat < 58.84763776) return 31;
                if (lat < 59.95459277) return 30;
                if (lat < 61.04917774) return 29;
                if (lat < 62.13216659) return 28;
                if (lat < 63.20427479) return 27;
                if (lat < 64.26616523) return 26;
                if (lat < 65.31845310) return 25;
                if (lat < 66.36171008) return 24;
                if (lat < 67.39646774) return 23;
                if (lat < 68.42322022) return 22;
                if (lat < 69.44242631) return 21;
                if (lat < 70.45451075) return 20;
                if (lat < 71.45986473) return 19;
                if (lat < 72.45884545) return 18;
                if (lat < 73.45177442) return 17;
                if (lat < 74.43893416) return 16;
                if (lat < 75.42056257) return 15;
                if (lat < 76.39684391) return 14;
                if (lat < 77.36789461) return 13;
                if (lat < 78.33374083) return 12;
                if (lat < 79.29428225) return 11;
                if (lat < 80.24923213) return 10;
                if (lat < 81.19801349) return 9;
                if (lat < 82.13956981) return 8;
                if (lat < 83.07199445) return 7;
                if (lat < 83.99173563) return 6;
                if (lat < 84.89166191) return 5;
                if (lat < 85.75541621) return 4;
                if (lat < 86.53536998) return 3;
                if (lat < 87.00000000) return 2;
                return 1;
            }

            private int cprModFunction(int a, int b)
            {
                int res = a % b;
                if (res < 0) res += b;
                return res;
            }

            private int cprNFunction(double lat, int isodd)
            {
                int nl = cprNLFunction(lat) - isodd;
                return nl < 1 ? 1 : nl;
            }

            private double cprDlonFunction(double lat, int isodd)
            {
                return 360.0 / cprNFunction(lat, isodd);
            }

            private void decodeCPR(Aircraft a)
            {
                const double AirDlat0 = 360.0 / 60.0;
                const double AirDlat1 = 360.0 / 59.0;

                double lat0 = a.even_cprlat;
                double lat1 = a.odd_cprlat;
                double lon0 = a.even_cprlon;
                double lon1 = a.odd_cprlon;

                int j = (int)Math.Floor(((59 * lat0 - 60 * lat1) / CPR_BITS) + 0.5);
                double rlat0 = AirDlat0 * (cprModFunction(j, 60) + lat0 / CPR_BITS);
                double rlat1 = AirDlat1 * (cprModFunction(j, 59) + lat1 / CPR_BITS);

                if (rlat0 >= 270.0) rlat0 -= 360.0;
                if (rlat1 >= 270.0) rlat1 -= 360.0;

                if (cprNLFunction(rlat0) != cprNLFunction(rlat1))
                    return;

                if (a.even_cprtime > a.odd_cprtime) {
                    int ni = cprNFunction(rlat0, 0);
                    int m = (int)Math.Floor((((lon0 * (cprNLFunction(rlat0) - 1)) -
                                               (lon1 * cprNLFunction(rlat0))) / CPR_BITS) + 0.5);
                    a.Longitude = cprDlonFunction(rlat0, 0) * (cprModFunction(m, ni) + lon0 / CPR_BITS);
                    a.Latitude = rlat0;
                } else {
                    int ni = cprNFunction(rlat1, 1);
                    int m = (int)Math.Floor((((lon0 * (cprNLFunction(rlat1) - 1)) -
                                               (lon1 * cprNLFunction(rlat1))) / (double)CPR_BITS) + 0.5);
                    a.Longitude = cprDlonFunction(rlat1, 1) * (cprModFunction(m, ni) + lon1 / CPR_BITS);
                    a.Latitude = rlat1;
                }

                if (a.Longitude > 180.0)
                    a.Longitude -= 360.0;
            }

            public void RawToAircraft(ModeSMessage mm, ref Aircraft a, long currentTime)
            {
                //long currentTime = TimeFunctions.GetCurrentTimeInMsec();
                a.LastSeen = currentTime;
                a.NumMessagesRaw++;

                if (mm.msg_type == 0 || mm.msg_type == 4 || mm.msg_type == 20) {
                    a.Altitude = mm.altitude;
                    a.HaveAltitude = true;
                } else if (mm.msg_type == 17) {
                    if (mm.ME_type >= 1 && mm.ME_type <= 4) {
                        string flight = (mm.flight ?? string.Empty).PadRight(8).Substring(0, 8);
                        a.FlightNum = flight.TrimEnd();
                        a.HaveFlightNum = true;
                    } else if (mm.ME_type >= 9 && mm.ME_type <= 18) {
                        a.Altitude = mm.altitude;
                        a.HaveAltitude = true;

                        if (mm.odd_flag != 0) {
                            a.odd_cprlat = mm.raw_latitude;
                            a.odd_cprlon = mm.raw_longitude;
                            a.odd_cprtime = currentTime;
                        } else {
                            a.even_cprlat = mm.raw_latitude;
                            a.even_cprlon = mm.raw_longitude;
                            a.even_cprtime = currentTime;
                        }

                        if (Math.Abs(a.even_cprtime - a.odd_cprtime) <= 10000) {
                            decodeCPR(a);
                            a.HaveLatLon = true;
                        }
                    } else if (mm.ME_type == 19) {
                        if (mm.ME_subtype == 1 || mm.ME_subtype == 2) {
                            a.Speed = mm.velocity;
                            a.Heading = mm.heading;
                            a.VerticalRate = (mm.vert_rate_sign == 0 ? 1 : -1) * (mm.vert_rate - 1) * 64;
                            a.HaveSpeedAndHeading = true;
                        }
                    }
                }
            }
        }
    }
}
