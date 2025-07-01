using System;

namespace ADS_B_Display.Models
{
    /// <summary>
    /// 항공기 등록 및 운영 데이터를 나타내는 클래스입니다.
    /// </summary>
    public class AircraftData
    {
        /// <summary>
        /// 항공기의 고유 ICAO 24비트 트랜스폰더 주소입니다.
        /// </summary>
        public uint Icao24 { get; set; }

        /// <summary>
        /// 항공기의 호출 부호(콜사인)입니다.
        /// </summary>
        public string Registration { get; set; }

        /// <summary>
        /// 항공기의 등록 번호입니다.
        /// </summary>
        public string ManufacturerIcao { get; set; }

        /// <summary>
        /// 항공기 제조업체 이름입니다.
        /// </summary>
        public string ManufacturerName { get; set; }

        /// <summary>
        /// 항공기 모델명입니다.
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 항공기 형식 코드입니다.
        /// </summary>
        public string Typecode { get; set; }

        /// <summary>
        /// 항공기의 일련번호(시리얼 넘버)입니다.
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// 생산 라인 번호입니다.
        /// </summary>
        public string LineNumber { get; set; }

        /// <summary>
        /// ICAO 항공기 형식 지정자입니다.
        /// </summary>
        public string IcaoAircraftType { get; set; }

        /// <summary>
        /// 항공기를 운영하는 기관 또는 회사입니다.
        /// </summary>
        public string Operator { get; set; }

        /// <summary>
        /// 운영사의 호출 부호(콜사인)입니다.
        /// </summary>
        public string OperatorCallsign { get; set; }

        /// <summary>
        /// 운영사의 ICAO 코드입니다.
        /// </summary>
        public string OperatorIcao { get; set; }

        /// <summary>
        /// 운영사의 IATA 코드입니다.
        /// </summary>
        public string OperatorIata { get; set; }

        /// <summary>
        /// 항공기 소유주입니다.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// 테스트 등록 여부를 나타냅니다.
        /// </summary>
        public bool TestReg { get; set; }

        /// <summary>
        /// 항공기가 등록된 날짜입니다. (값이 없을 수 있음)
        /// </summary>
        public DateTime? Registered { get; set; }

        /// <summary>
        /// 등록 만료일입니다. (값이 없을 수 있음)
        /// </summary>
        public DateTime? RegUntil { get; set; }

        /// <summary>
        /// 항공기의 현재 상태입니다. (예: Active, Stored)
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 항공기가 제작된 연도입니다. (값이 없을 수 있음)
        /// </summary>
        public int? Built { get; set; }

        /// <summary>
        /// 첫 비행 날짜입니다. (값이 없을 수 있음)
        /// </summary>
        public DateTime? FirstFlightDate { get; set; }

        /// <summary>
        /// 좌석 배치 정보입니다. (예: "Y180")
        /// </summary>
        public string SeatConfiguration { get; set; }

        /// <summary>
        /// 엔진 구성 정보입니다.
        /// </summary>
        public string Engines { get; set; }

        /// <summary>
        /// Mode-S 지원 여부를 나타냅니다.
        /// </summary>
        public bool Modes { get; set; }

        /// <summary>
        /// ADS-B 지원 여부를 나타냅니다.
        /// </summary>
        public bool Adsb { get; set; }

        /// <summary>
        /// ACARS 지원 여부를 나타냅니다.
        /// </summary>
        public bool Acars { get; set; }

        /// <summary>
        /// 기타 메모 또는 비고 사항입니다.
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// 항공기 분류 카테고리입니다.
        /// </summary>
        public string CategoryDescription { get; set; }

        /// <summary>
        /// 항공기에 대한 국가 설명입니다.
        /// </summary>
        public string Country { get; set; }
        public string CountryShort { get; set; }

        /// <summary>
        /// 항공기에 대한 Militery 설명입니다.
        /// </summary>
        public bool IsMilitary { get; set; }

        public int AircraftImageNum { get; set; }
    }
}
