using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display
{
    internal class MathExt
    {
        public static double Hypot<T>(T x, T y)
        {
            double xd = Convert.ToDouble(x);
            double yd = Convert.ToDouble(y);

            xd = Math.Abs(xd);
            yd = Math.Abs(yd);

            if (xd < yd) {
                double temp = xd;
                xd = yd;
                yd = temp;
            }

            if (xd == 0.0)
                return 0.0;

            double r = yd / xd;
            return xd * Math.Sqrt(1 + r * r);
        }

        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;
            return value;
        }

        public static double Asinh(double value)
        {
            return Math.Log(value + Math.Sqrt(value * value + 1.0));
        }
    }
}
