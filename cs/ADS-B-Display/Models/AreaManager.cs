using ADS_B_Display;
using ADS_B_Display.Models.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

internal class AreaManager
{
    private static List<Area> _areas = new List<Area>();
    private static Area _tempArea = null;

    public static IReadOnlyList<Area> Areas => _areas.AsReadOnly();
    public static Area TempArea => _tempArea;
    public static bool IsInsertMode
    {
        get => _isInsertMode;
        set => _isInsertMode = value;
    }
    public static bool UsePolygon { get; set; }

    private static bool _isInsertMode;

    static AreaManager()
    {
        ResetTempArea();
        _isInsertMode = false;
    }

    public static void LoadArea(List<Area> areas)
    {
        _areas = areas.Where(area => area != null).ToList() ?? new List<Area>();
    }

    public static void AddArea(Area area)
    {
        if (area != null)
            _areas.Add(area);
    }

    public static void RemoveArea(Area area)
    {
        if (area != null)
        {
            _areas.Remove(area);
        }
    }

    public static void ClearAreas()
    {
        _areas.Clear();
    }

    public static bool AddPointToTempArea(double lat, double lon)
    {
        if(_tempArea == null)
            _tempArea = new Area();

        if (_tempArea == null || _tempArea.NumPoints >= Area.MAX_AREA_POINTS)
            return false;

        _tempArea.Points.Add(new OpenTK.Vector3d(lon, lat, 0.0));

        _tempArea.NumPoints++;

        return true;
    }

    public static bool FinalizeTempAreaIfReady(string areaName, Color color)
    {
        if (Area.Finalize(ref _tempArea, areaName, color))
        {
            AddArea(_tempArea);
            ResetTempArea();
            return true;
        }
        
        return false;
    }

    public static void ResetTempArea()
    {
        _tempArea = new Area
        {
            NumPoints = 0,
            Name = "TempArea",
            Selected = false,
            Triangles = null
        }; 
    }

    public static int Orientation2DPolygon(Area area)
    {
        if (area == null || area.NumPoints < 3)
            return 0;

        int count = 0;
        int n = area.NumPoints;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            int k = (i + 2) % n;

            var pi = area.Points[i];
            var pj = area.Points[j];
            var pk = area.Points[k];

            double z = (pj.X - pi.X) * (pk.Y - pj.Y) -
                       (pj.Y - pi.Y) * (pk.X - pj.X);

            if (z < 0)
                count--;
            else if (z > 0)
                count++;
        }

        if (count > 0)
            return -1; // COUNTERCLOCKWISE
        else if (count < 0)
            return 1;  // CLOCKWISE
        else
            return 0;  // Degenerate
    }

}
