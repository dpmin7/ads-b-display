using System.Collections.Generic;
using System.Linq;

namespace ADS_B_Display.Models
{
    internal static class AircraftManager
    {
        private static readonly Dictionary<uint, Aircraft> _aircraftTable
            = new Dictionary<uint, Aircraft>();

        internal static Aircraft GetOrAdd(uint icao)
        {
            if (!_aircraftTable.TryGetValue(icao, out var aircraft)) {
                aircraft = new Aircraft {
                    ICAO = icao,
                    HexAddr = icao.ToString("X6"),
                    NumMessagesSBS = 0,
                    NumMessagesRaw = 0,
                    VerticalRate = 0,
                    HaveAltitude = false,
                    HaveLatLon = false,
                    HaveSpeedAndHeading = false,
                    HaveFlightNum = false,
                    // SpriteImage 및 CycleImages 로직은 UI 쪽에서 설정
                };
                _aircraftTable[icao] = aircraft;
            }
            return aircraft;
        }

        internal static bool TryGet(uint icao, out Aircraft aircraft)
        {
            return _aircraftTable.TryGetValue(icao, out aircraft);
        }

        internal static IEnumerable<Aircraft> GetAll()
            => _aircraftTable.Values.ToList();

        internal static uint ReceiveSBSMessage(string msgLine)
        {
            // 필드를 분리
            string[] SBS_Fields = SBSMessage.SplitSbsFields(msgLine);
            if (SBSMessage.VerifySbsAndIcao(SBS_Fields, out uint addr) == false) {
                return 0; // 유효하지 않은 SBS 메시지
            }
            var aircraft = GetOrAdd(addr); // Aircraft 객체를 가져오거나 생성
            SBSMessage.SBS_Message_Decode(SBS_Fields, ref aircraft);

            return addr;
        }

        internal static uint ReceiveRawMessage(string msgLine)
        {
            ModeSMessage modeSMessage = new ModeSMessage();
            var status = DecodeRawAdsB.Decode_RAW_message(msgLine, ref modeSMessage);

            if (status != TDecodeStatus.HaveMsg) {
                return 0; // 유효하지 않은 Raw 메시지
            } else if (status == TDecodeStatus.MsgHeartBeat) {
                return 0;
            }

            uint addr = (uint)((modeSMessage.AA[0] << 16) | (modeSMessage.AA[1] << 8) | modeSMessage.AA[2]);
            var aircraft = GetOrAdd(addr); // Aircraft 객체를 가져오거나 생성
            AircraftDecoder.RawToAircraft(modeSMessage, ref aircraft);
            
            return addr; // Raw 메시지 처리는 아직 구현되지 않음
        }
    }
}
