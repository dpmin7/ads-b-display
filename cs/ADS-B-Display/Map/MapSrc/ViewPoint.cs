using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    public class Eye
    {
        public double X { get; set; }      // longitude [-0.5..0.5]
        public double Y { get; set; }      // latitude  [-0.25..0.25]
        public double H { get; set; }      // height above surface
        public double Orient { get; set; } // orientation [0..2*PI]
        public double Pitch { get; set; }  // pitch [0..PI/2]
        public double Fov { get; set; }    // field-of-view (radians)

        public Eye()
        {
            X = 0.0;
            Y = 0.0;
            H = 1.0;
            Orient = 0.0;
            Pitch = 0.0;
            Fov = Math.PI / 2;
        }

        public Eye(double nh, double nfov)
        {
            X = 0.0;
            Y = 0.0;
            H = nh;
            Orient = 0.0;
            Pitch = 0.0;
            Fov = nfov;
        }

        public Eye(double nx, double ny, double nh, double norient, double npitch, double nfov)
        {
            X = nx;
            Y = ny;
            H = nh;
            Orient = norient;
            Pitch = npitch;
            Fov = nfov;
        }

        public static Eye operator +(Eye a, Eye b)
        {
            return new Eye(
                a.X + b.X,
                a.Y + b.Y,
                a.H + b.H,
                a.Orient + b.Orient,
                a.Pitch + b.Pitch,
                a.Fov + b.Fov
            );
        }

        public static Eye operator *(Eye a, double k)
        {
            return new Eye(
                a.X * k,
                a.Y * k,
                a.H * k,
                a.Orient * k,
                a.Pitch * k,
                a.Fov * k
            );
        }

        public double XSpan(double aspect)
        {
            return aspect * 2.0 * Math.Tan(Fov / 2.0) * H / Math.Sqrt(1.0 + aspect * aspect);
        }

        public double YSpan(double aspect)
        {
            return 2.0 * Math.Tan(Fov / 2.0) * H / Math.Sqrt(1.0 + aspect * aspect);
        }

        public double Span()
        {
            return 2.0 * Math.Tan(Fov / 2.0) * H;
        }

        public double XFov(double aspect)
        {
            return 2.0 * Math.Atan2(XSpan(aspect) / 2.0, H);
        }

        public double YFov(double aspect)
        {
            return 2.0 * Math.Atan2(YSpan(aspect) / 2.0, H);
        }
    }

    public class Viewpoint
    {
        private const double DEFAULT_TRANSLATION_TIME = 1.0;

        private Eye m_CurrentEye;
        private Eye m_SourceEye;
        private Eye m_TargetEye;
        private double m_Translation;
        private double m_TranslationTime;

        public Viewpoint()
        {
            m_Translation = 0.0;
            m_TranslationTime = DEFAULT_TRANSLATION_TIME;
            m_CurrentEye = new Eye();
            m_SourceEye = new Eye();
            m_TargetEye = new Eye();
        }

        public Viewpoint(Eye eye)
        {
            m_CurrentEye = new Eye(eye.X, eye.Y, eye.H, eye.Orient, eye.Pitch, eye.Fov);
            m_SourceEye = new Eye(eye.X, eye.Y, eye.H, eye.Orient, eye.Pitch, eye.Fov);
            m_TargetEye = new Eye(eye.X, eye.Y, eye.H, eye.Orient, eye.Pitch, eye.Fov);

            m_Translation = 1.0;
            m_TranslationTime = DEFAULT_TRANSLATION_TIME;
        }

        public void SetCurrentCoordinates(Eye eye)
        {
            m_CurrentEye = new Eye(eye.X, eye.Y, eye.H, eye.Orient, eye.Pitch, eye.Fov);
            m_SourceEye = new Eye(eye.X, eye.Y, eye.H, eye.Orient, eye.Pitch, eye.Fov);
            m_TargetEye = new Eye(eye.X, eye.Y, eye.H, eye.Orient, eye.Pitch, eye.Fov);

            m_Translation = 1.0;
        }

        public void SetTargetCoordinates(Eye eye)
        {
            m_SourceEye = new Eye(m_CurrentEye.X, m_CurrentEye.Y, m_CurrentEye.H, m_CurrentEye.Orient, m_CurrentEye.Pitch, m_CurrentEye.Fov);
            m_TargetEye = new Eye(eye.X, eye.Y, eye.H, eye.Orient, eye.Pitch, eye.Fov);

            m_Translation = 0.0;
        }

        public void Animate(double delta)
        {
            if (m_Translation >= 1.0)
                return;

            m_Translation += delta / m_TranslationTime;

            if (m_Translation >= 1.0) {
                m_Translation = 1.0;
                m_CurrentEye = new Eye(
                    m_TargetEye.X, m_TargetEye.Y, m_TargetEye.H,
                    m_TargetEye.Orient, m_TargetEye.Pitch, m_TargetEye.Fov
                );
                return;
            }

            double trans = TranslationFunction(m_Translation);
            Eye interpEye = m_SourceEye * (1.0 - trans) + m_TargetEye * trans;
            m_CurrentEye = interpEye;
        }

        private double TranslationFunction(double percent)
        {
            // 1 - (1 - x)^2 형태
            return percent * (2.0 - percent);
        }

        public Eye GetEye()
        {
            return new Eye(
                m_CurrentEye.X, m_CurrentEye.Y, m_CurrentEye.H,
                m_CurrentEye.Orient, m_CurrentEye.Pitch, m_CurrentEye.Fov
            );
        }
    }
}
