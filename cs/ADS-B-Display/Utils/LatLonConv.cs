using System;

namespace ADS_B_Display
{
    public enum TCoordConvStatus
    {
        OKNOERROR = 0,
        LONGERR = 1,
        LATERR = 2,
        ANTIPODAL = 3,
        SAMEPT = 4,
        ZERODIST = 5,
        NOCONVERGE = 6
    }

    public static class LatLonConv
    {
        private const double M_PI = Math.PI;
        private const double METERS_PER_NAUTICAL_MILE = 1852.0;
        private const double EllipseMajorInMeters = 6378137.000;
        private const double EllipseMinorInMeters = 6356752.314;
        private const double EllipseMajor = EllipseMajorInMeters / METERS_PER_NAUTICAL_MILE;
        private const double EllipseMinor = EllipseMinorInMeters / METERS_PER_NAUTICAL_MILE;
        private const double EPS = 0.00000000000005;

        private static double DegToRad(double deg) => (M_PI / 180.0) * deg;
        private static double RadToDeg(double rad) => (180.0 / M_PI) * rad;

        public static TCoordConvStatus VInverse(double lat1, double lon1, double lat2, double lon2, out double dist, out double az1, out double az2)
        {
            dist = az1 = az2 = 0.0;
            int icount = 0, MAXCT = 100;
            double a0 = EllipseMajor, b0 = EllipseMinor;
            double flat = (a0 - b0) / a0, r = 1.0 - flat;
            double u1 = Math.Atan(r * Math.Tan(DegToRad(lat1)));
            double u2 = Math.Atan(r * Math.Tan(DegToRad(lat2)));
            double sinu1 = Math.Sin(u1), cosu1 = Math.Cos(u1);
            double sinu2 = Math.Sin(u2), cosu2 = Math.Cos(u2);
            double L = DegToRad(lon2 - lon1), lambda = L, testlambda;

            if (IsAntipodal(lat1, lat2, lon1, lon2)) return TCoordConvStatus.ANTIPODAL;
            if (Math.Abs(lat1 - lat2) < 1e-6 && Math.Abs(lon1 - lon2) < 1e-6) return TCoordConvStatus.SAMEPT;

            double sigma, cs, ss, sinalpha, cosalpha, c2sm, C, A, B, dsigma;
            do {
                icount++;
                testlambda = lambda;

                double sinlambda = Math.Sin(lambda);
                double coslambda = Math.Cos(lambda);
                ss = Math.Sqrt(Sqr(cosu2 * sinlambda) + Sqr(cosu1 * sinu2 - sinu1 * cosu2 * coslambda));
                cs = sinu1 * sinu2 + cosu1 * cosu2 * coslambda;
                sigma = Math.Atan2(ss, cs);

                sinalpha = cosu1 * cosu2 * sinlambda / ss;
                cosalpha = Math.Sqrt(1 - Sqr(sinalpha));

                c2sm = cs - (2 * sinu1 * sinu2 / Sqr(cosalpha));
                C = flat / 16.0 * Sqr(cosalpha) * (4 + flat * (4 - 3 * Sqr(cosalpha)));
                lambda = L + (1 - C) * flat * sinalpha *
                         (sigma + C * ss * (c2sm + C * cs * (-1 + 2 * Sqr(c2sm))));

            } while (Math.Abs(testlambda - lambda) > EPS && icount <= MAXCT);

            if (icount > MAXCT) return TCoordConvStatus.NOCONVERGE;

            u2 = Sqr(cosalpha) * (Sqr(a0) - Sqr(b0)) / Sqr(b0);
            A = 1 + (u2 / 16384.0) * (4096.0 + u2 * (-768.0 + u2 * (320.0 - 175.0 * u2)));
            B = (u2 / 1024.0) * (256.0 + u2 * (-128.0 + u2 * (74.0 - 47.0 * u2)));

            dsigma = B * ss * (c2sm + (B / 4.0) *
                      (cs * (-1 + 2 * Sqr(c2sm)) - (B / 6.0) * c2sm *
                      (-3 + 4 * Sqr(ss)) * (-3 + 4 * Sqr(c2sm))));

            dist = b0 * A * (sigma - dsigma);

            az1 = RadToDeg(ModAzimuth(Math.Atan2(cosu2 * Math.Sin(lambda),
                     (cosu1 * sinu2 - sinu1 * cosu2 * Math.Cos(lambda)))));

            az2 = RadToDeg(ModAzimuth(Math.Atan2(cosu1 * Math.Sin(lambda),
                     (-sinu1 * cosu2 + cosu1 * sinu2 * Math.Cos(lambda))) - M_PI));

            return TCoordConvStatus.OKNOERROR;
        }

        public static TCoordConvStatus VDirect(double lat1, double lon1, double az1, double dist, out double lat2, out double lon2, out double az2)
        {
            lat2 = lon2 = az2 = 0.0;
            if (dist == 0.0) {
                lat2 = lat1;
                lon2 = lon1;
                return TCoordConvStatus.ZERODIST;
            }

            double a0 = EllipseMajor, b0 = EllipseMinor;
            double flat = (a0 - b0) / a0, r = 1.0 - flat;

            double u1 = Math.Atan(r * Math.Tan(DegToRad(lat1)));
            double sigma1 = Math.Atan(Math.Tan(u1) / Math.Cos(DegToRad(az1)));
            double sinalpha = Math.Cos(u1) * Math.Sin(DegToRad(az1));
            double cosalpha = Math.Sqrt(1.0 - Sqr(sinalpha));
            double usqr = Sqr(cosalpha) * (Sqr(a0) - Sqr(b0)) / Sqr(b0);

            double A = 1.0 + usqr / 16384.0 * (4096.0 + usqr * (-768.0 + usqr * (320.0 - 175.0 * usqr)));
            double B = usqr / 1024.0 * (256.0 + usqr * (-128.0 + usqr * (74.0 - 47.0 * usqr)));

            double sigma = dist / (b0 * A), deltasigma;
            double sigmaPrev;
            double c2sm;
            do {
                sigmaPrev = sigma;
                double twosigmam = 2.0 * sigma1 + sigma;
                double ss = Math.Sin(sigma), cs = Math.Cos(sigma);
                c2sm = Math.Cos(twosigmam);

                deltasigma = B * ss * (c2sm + B / 4.0 * (cs * (-1.0 + 2.0 * Sqr(c2sm)) -
                    B / 6.0 * c2sm * (-3.0 + 4.0 * Sqr(ss)) * (-3.0 + 4.0 * Sqr(c2sm))));

                sigma = dist / (b0 * A) + deltasigma;
            }
            while (Math.Abs(sigma - sigmaPrev) > EPS);

            double lat = Math.Atan2(Math.Sin(u1) * Math.Cos(sigma) + Math.Cos(u1) * Math.Sin(sigma) * Math.Cos(DegToRad(az1)),
                          r * Math.Sqrt(Sqr(sinalpha) + Sqr(Math.Sin(u1) * Math.Sin(sigma) - Math.Cos(u1) * Math.Cos(sigma) * Math.Cos(DegToRad(az1)))));

            double lambda = Math.Atan2(Math.Sin(sigma) * Math.Sin(DegToRad(az1)),
                             Math.Cos(u1) * Math.Cos(sigma) - Math.Sin(u1) * Math.Sin(sigma) * Math.Cos(DegToRad(az1)));

            double C = flat / 16.0 * Sqr(cosalpha) * (4.0 + flat * (4.0 - 3.0 * Sqr(cosalpha)));
            double omega = lambda - (1.0 - C) * flat * sinalpha *
                           (sigma + C * Math.Sin(sigma) * (c2sm + C * Math.Cos(sigma) * (-1.0 + 2.0 * Sqr(c2sm))));

            lon2 = RadToDeg(ModLongitude(DegToRad(lon1) + omega));
            lat2 = RadToDeg(ModLatitude(lat));

            az2 = RadToDeg(ModAzimuth(Math.Atan2(sinalpha, -Math.Sin(u1) * Math.Sin(sigma) + Math.Cos(u1) * Math.Cos(sigma) * Math.Cos(DegToRad(az1))) - M_PI));
            return TCoordConvStatus.OKNOERROR;
        }

        private static double Sqr(double x) => x * x;
        private static double ModAzimuth(double az) => Modulus(az, 2.0 * M_PI);
        private static double ModLatitude(double lat) => Modulus(lat + M_PI / 2.0, M_PI) - M_PI / 2.0;
        private static double ModLongitude(double lon) => Modulus(lon + M_PI, 2.0 * M_PI) - M_PI;

        private static double Modulus(double x, double y) => x - y * Math.Floor(x / y);

        private static bool IsAntipodal(double lat1, double lat2, double lon1, double lon2)
        {
            Antipod(lat1, lon1, out var la, out var lo);
            return Math.Abs(lat2 - la) < 1e-6 && Math.Abs(lon2 - lo) < 1e-6;
        }

        private static TCoordConvStatus Antipod(double latIn, double lonIn, out double latOut, out double lonOut)
        {
            latOut = lonOut = 9999.0;
            if (Math.Abs(lonIn) > 180.0) return TCoordConvStatus.LONGERR;
            if (Math.Abs(latIn) > 90.0) return TCoordConvStatus.LATERR;

            latOut = -latIn;
            lonOut = Modulus(lonIn + 180.0, 180.0);
            return TCoordConvStatus.OKNOERROR;
        }
    }
}
