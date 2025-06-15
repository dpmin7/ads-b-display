using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ADS_B_Display
{
    /// <summary>
    /// 다각형 영역 데이터.
    /// 원본 TArea 구조체를 C#으로 변환.
    /// </summary>
    public class Area
    {
        public const int MAX_AREA_POINTS = 500;

        /// <summary>
        /// 영역 이름
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 영역 색상
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// 정의된 점 개수
        /// </summary>
        public int NumPoints { get; set; }

        /// <summary>
        /// 영역 점들의 위경도 좌표 배열 (Lat, Lon, Hae)
        /// </summary>
        public Vector3d[] Points { get; }

        /// <summary>
        /// 투영된 점들의 화면 좌표 배열
        /// </summary>
        public Vector3d[] PointsAdj { get; }

        /// <summary>
        /// 삼각형 분할 결과 리스트
        /// </summary>
        public TTriangles Triangles { get; set; }

        /// <summary>
        /// 리스트에서 선택된 상태
        /// </summary>
        public bool Selected { get; set; }

        public Area()
        {
            Name = string.Empty;
            Color = Colors.White;
            NumPoints = 0;
            Points = new Vector3d[MAX_AREA_POINTS];
            PointsAdj = new Vector3d[MAX_AREA_POINTS];
            Triangles = null;
            Selected = false;
        }
    }

    /// <summary>
    /// 삼각형 분할된 인덱스 정보를 담는 링크드 리스트 노드
    /// </summary>
    public class TTriangles
    {
        /// <summary>정점 인덱스 배열 (항상 길이 3)</summary>
        public long[] IndexList { get; set; }

        /// <summary>다음 삼각형 노드</summary>
        public TTriangles Next { get; set; }

        public TTriangles(long i0, long i1, long i2)
        {
            IndexList = new[] { i0, i1, i2 };
            Next = null;
        }
    }
}
