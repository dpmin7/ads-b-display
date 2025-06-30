using ADS_B_Display.Models.Settings;
using OpenTK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        public bool Use { get; set; } = true;
        public string Name { get; set; }
        public Color Color { get; set; }
        public string ColorStr => Color.ToString();
        public int NumPoints { get; set; }
        public List<Vector3d> Points { get; set; }
        public Vector3d[] PointsAdj { get; }
        public TTriangles Triangles { get; set; }
        public bool Selected { get; set; }

        public string AreaName => Name;

        private readonly HashSet<Aircraft> _aircraftSetInArea = new HashSet<Aircraft>();
        public IReadOnlyCollection<Aircraft> AircraftListInArea => _aircraftSetInArea;

        public SolidColorBrush AreaColorBrush => new SolidColorBrush(Color);

        public Area()
        {
            Name = string.Empty;
            Color = Colors.White;
            NumPoints = 0;
            Points = new List<Vector3d>();
            Triangles = null;
            Selected = false;
        }

        public bool AddAircraft(Aircraft aircraft)
        {
            aircraft.AreaName = Name;
            if (aircraft != null)
                return _aircraftSetInArea.Add(aircraft); // 자동으로 중복 제거
            return  false;
        }

        public void RemoveAircraft(Aircraft aircraft)
        {
            if (aircraft != null)
                _aircraftSetInArea.Remove(aircraft);
        }

        public void ClearAircrafts() => _aircraftSetInArea.Clear();

        public bool ContainsAircraft(Aircraft aircraft)
        {
            return _aircraftSetInArea.Contains(aircraft);
        }

        public static Area AreaConfigToArea(AreaOfInterest areaConfig)
        {
            if (areaConfig == null) return null;
            var color = (Color)ColorConverter.ConvertFromString(areaConfig.Color);
            var area = new Area
            {
                Use = areaConfig.Use,
                Points = areaConfig.Area.Select(p => new Vector3d(p.X, p.Y, 0)).ToList(),
                NumPoints = areaConfig.Area.Count
            };

            if(Finalize(ref area, areaConfig.Name, color))
            {   
                return area;
            }

            return null;
        }

        private const int minPoints = 3;
        public static bool Finalize(ref Area tempArea, string name, Color color)
        {
            if (tempArea != null && tempArea.NumPoints >= minPoints)
            {
                tempArea.Triangles = TrianglePoly.TriangulatePolygon(tempArea.Points.ToArray());
                tempArea.Name = name;
                tempArea.Color = color;
                return true;
            }

            return false;
        }

        public void SetViewable(bool set)
        {
            foreach (var aircraft in _aircraftSetInArea)
            {
                aircraft.Viewable = set;
            }
        }
    }

    /// <summary>
    /// 삼각형 분할된 인덱스 정보를 담는 링크드 리스트 노드
    /// </summary>
    public class TTriangles : IEnumerable<long[]>
    {
        public long[] IndexList { get; set; }
        public TTriangles Next { get; set; }

        public TTriangles(long i0, long i1, long i2)
        {
            IndexList = new[] { i0, i1, i2 };
            Next = null;
        }

        public IEnumerator<long[]> GetEnumerator()
        {
            TTriangles current = this;
            while (current != null)
            {
                yield return current.IndexList;
                current = current.Next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
