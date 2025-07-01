using ControlzEx.Standard;
using System;

namespace ADS_B_Display.Models.CPA
{
    public class CPACsComputer : ICPAComputer
    {
        private const double InvalidValue = -1;

        /// <summary>
        /// 두 항공기 간 CPA를 계산합니다.
        /// </summary>
        /// <param name="ac1">첫 번째 항공기</param>
        /// <param name="ac2">두 번째 항공기</param>
        /// <returns>CPA 거리(m)와 시간(s). 유효하지 않으면 (-1, -1) 반환</returns>
        public bool ComputeCPA(Aircraft ac1, Aircraft ac2, out double tcpa, out double cpa_distance_nm, out double vertical_cpa)
        {
            if (!IsValidAircraft(ac1) || !IsValidAircraft(ac2))
            {
                tcpa = -1;
                cpa_distance_nm = -1;
                vertical_cpa = -1;
                return false;
            }

            return ClosestApproachCalculator.FindClosestDistance(ac1, ac2, out tcpa, out cpa_distance_nm, out vertical_cpa);
        }

        private static bool IsValidAircraft(Aircraft ac)
        {
            return ac != null && ac.Speed > 0 && ac.Altitude > 0;
        }
    }

    public class Vector3D
    {
        public double X, Y, Z;

        public Vector3D(double x, double y, double z)
        {
            X = x; Y = y; Z = z;
        }

        public static Vector3D operator +(Vector3D a, Vector3D b)
            => new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vector3D operator -(Vector3D a, Vector3D b)
            => new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3D operator *(Vector3D a, double scalar)
            => new Vector3D(a.X * scalar, a.Y * scalar, a.Z * scalar);

        public double Dot(Vector3D other)
            => X * other.X + Y * other.Y + Z * other.Z;

        public double Magnitude()
            => Math.Sqrt(X * X + Y * Y + Z * Z);
    }

    public static class ClosestApproachCalculator
    {
        private const double EarthRadius = 6371000.0; // meters
        private const double KnotToMs = 0.514444;
        private const double FtPerMinToMs = 0.00508;

        public static bool FindClosestDistance(Aircraft ac1, Aircraft ac2, out double tcpa, out double cpa_distance_nm, out double vertical_cpa)
        {
            const double MetersToNauticalMiles = 1.0 / 1852.0;
            const double MetersToFeet = 3.28084;

            var pos1 = GeoToXYZ(ac1.VLatitude, ac1.VLongitude, ac1.Altitude);
            var pos2 = GeoToXYZ(ac2.VLatitude, ac2.VLongitude, ac2.Altitude);

            var vel1 = VelocityVector(ac1);
            var vel2 = VelocityVector(ac2);

            var deltaPos = pos1 - pos2;
            var deltaVel = vel1 - vel2;

            double tStar = -deltaPos.Dot(deltaVel) / deltaVel.Dot(deltaVel);
            tcpa = Clamp(tStar, 0, 30); // Clamp to [0, 30]

            var closestP1 = pos1 + vel1 * tStar;
            var closestP2 = pos2 + vel2 * tStar;

            cpa_distance_nm = (closestP1 - closestP2).Magnitude() * MetersToNauticalMiles;

            vertical_cpa = Math.Abs(closestP1.Z - closestP2.Z) * MetersToFeet;

            return true;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static Vector3D VelocityVector(Aircraft ac)
        {
            double headingRad = ac.Heading * Math.PI / 180.0;
            double speedMs = ac.Speed * KnotToMs;
            double vRateMs = ac.VerticalRate * FtPerMinToMs;

            double vx = speedMs * Math.Cos(headingRad);
            double vy = speedMs * Math.Sin(headingRad);
            double vz = vRateMs;

            return new Vector3D(vx, vy, vz);
        }

        private static Vector3D GeoToXYZ(double latDeg, double lonDeg, double altMeters)
        {
            double latRad = latDeg * Math.PI / 180.0;
            double lonRad = lonDeg * Math.PI / 180.0;

            double cosLat = Math.Cos(latRad);
            double sinLat = Math.Sin(latRad);
            double cosLon = Math.Cos(lonRad);
            double sinLon = Math.Sin(lonRad);

            double radius = EarthRadius + altMeters;

            double x = radius * cosLat * cosLon;
            double y = radius * cosLat * sinLon;
            double z = radius * sinLat;

            return new Vector3D(x, y, z);
        }
    }
}