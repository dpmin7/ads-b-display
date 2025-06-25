using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;

namespace ADS_B_Display.Utils
{
    internal class AltitudeToColor
    {
        private static List<(double altitude, (double r, double g, double b))> stops = new List<(double altitude, (double r, double g, double b))>
        {
            (0.0,      (1.0, 0.27, 0.0)),     // OrangeRed
            (1000.0,   (1.0, 0.55, 0.0)),     // Orange
            (4000.0,   (1.0, 1.0, 0.0)),      // Yellow
            (10000.0,  (0.0, 1.0, 0.0)),      // Lime
            (20000.0,  (0.0, 1.0, 1.0)),      // Cyan
            (30000.0,  (0.0, 0.0, 1.0)),      // Blue
            (40000.0,  (0.78, 0.08, 0.52))    // MediumVioletRed
        };

        public static (double r, double g, double b) GetAltitudeColorRGB(double altitudeFt)
        {
            if (altitudeFt <= stops[0].altitude)
                return stops[0].Item2;
            if (altitudeFt >= stops[stops.Count - 1].altitude)
                return stops[stops.Count - 1].Item2;

            for (int i = 0; i < stops.Count - 1; i++) {
                var (alt1, color1) = stops[i];
                var (alt2, color2) = stops[i + 1];
                if (altitudeFt >= alt1 && altitudeFt <= alt2) {
                    double t = (altitudeFt - alt1) / (alt2 - alt1);
                    double r = color1.r + (color2.r - color1.r) * t;
                    double g = color1.g + (color2.g - color1.g) * t;
                    double b = color1.b + (color2.b - color1.b) * t;
                    return (r, g, b);
                }
            }

            return (0.5, 0.5, 0.5); // fallback: gray
        }
    }
}
