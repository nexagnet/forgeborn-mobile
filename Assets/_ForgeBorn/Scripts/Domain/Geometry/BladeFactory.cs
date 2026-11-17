using System;

namespace ForgeBorn.Domain.Geometry
{
    /// <summary>Builds starting blanks. Rings are CCW in (Y,Z) and always contain the four rectangle corners (P must be a multiple of 4).</summary>
    public static class BladeFactory
    {
        public static BladeProfile CreateRectBlank(double lengthM, double widthM, double thicknessM, int stations, int perimeter)
        {
            return CreateTapered(lengthM, x => widthM * 0.5, x => thicknessM * 0.5, stations, perimeter);
        }

        /// <summary>halfWidth(x) and halfThickness(x) take x in meters [0, length].</summary>
        public static BladeProfile CreateTapered(double lengthM, Func<double, double> halfWidth, Func<double, double> halfThickness,
            int stations, int perimeter)
        {
            if (lengthM <= 0.0) throw new ArgumentException("length must be > 0");
            if (stations < 2) throw new ArgumentException("stations must be >= 2");
            if (perimeter < 4 || perimeter % 4 != 0) throw new ArgumentException("perimeter must be a multiple of 4");

            var x = new double[stations];
            var ring = new double[stations * perimeter * 2];
            var severed = new bool[stations];
            int n = perimeter / 4;

            for (int s = 0; s < stations; s++)
            {
                double xs = lengthM * s / (stations - 1);
                x[s] = xs;
                double hw = halfWidth(xs);
                double ht = halfThickness(xs);
                if (!(hw > 0.0) || !(ht > 0.0)) throw new ArgumentException("half extents must be > 0 at x=" + xs);

                int b = s * perimeter * 2;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / n;
                    Set(ring, b, i, -hw + 2.0 * hw * t, -ht);          // bottom: left -> right
                    Set(ring, b, n + i, hw, -ht + 2.0 * ht * t);       // right: bottom -> top
                    Set(ring, b, 2 * n + i, hw - 2.0 * hw * t, ht);    // top: right -> left
                    Set(ring, b, 3 * n + i, -hw, ht - 2.0 * ht * t);   // left: top -> bottom
                }
            }
            return new BladeProfile(x, ring, severed, perimeter);
        }

        /// <summary>
        /// Single-edged blank: full thickness on the -Y side (spine), tapering over bevelWidthM to edgeThicknessM at the +Y edge.
        /// Choose bevelWidthM as a whole multiple of widthM/(perimeter/4) so the bevel kink lands exactly on a ring vertex.
        /// </summary>
        public static BladeProfile CreateBeveled(double lengthM, double widthM, double thicknessM, double bevelWidthM,
            double edgeThicknessM, int stations, int perimeter)
        {
            if (lengthM <= 0.0 || widthM <= 0.0 || thicknessM <= 0.0) throw new ArgumentException("dimensions must be > 0");
            if (!(bevelWidthM > 0.0 && bevelWidthM < widthM)) throw new ArgumentException("bevel width must be inside the blade width");
            if (!(edgeThicknessM > 0.0 && edgeThicknessM < thicknessM)) throw new ArgumentException("edge thickness must be < thickness");
            if (stations < 2) throw new ArgumentException("stations must be >= 2");
            if (perimeter < 4 || perimeter % 4 != 0) throw new ArgumentException("perimeter must be a multiple of 4");

            double hw = widthM * 0.5, ht = thicknessM * 0.5, te = edgeThicknessM * 0.5;
            int n = perimeter / 4;
            var x = new double[stations];
            var ring = new double[stations * perimeter * 2];
            var severed = new bool[stations];
            for (int s = 0; s < stations; s++)
            {
                x[s] = lengthM * s / (stations - 1);
                int b = s * perimeter * 2;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / n;
                    double yb = -hw + 2.0 * hw * t;
                    Set(ring, b, i, yb, -HalfThickness(yb, hw, ht, te, bevelWidthM));
                    Set(ring, b, n + i, hw, -te + 2.0 * te * t);
                    double yt = hw - 2.0 * hw * t;
                    Set(ring, b, 2 * n + i, yt, HalfThickness(yt, hw, ht, te, bevelWidthM));
                    Set(ring, b, 3 * n + i, -hw, ht - 2.0 * ht * t);
                }
            }
            return new BladeProfile(x, ring, severed, perimeter);
        }

        private static double HalfThickness(double y, double hw, double ht, double te, double bevelWidthM)
        {
            double d = hw - y;
            return d >= bevelWidthM ? ht : te + (ht - te) * d / bevelWidthM;
        }

        /// <summary>Sets the section frames from functions of x (m from the tang end). Null functions leave that channel unchanged.</summary>
        public static void ApplyFrame(BladeProfile p, Func<double, double> offsetY, Func<double, double> offsetZ, Func<double, double> twistRad)
        {
            for (int s = 0; s < p.StationCount; s++)
            {
                double x = p.X[s] - p.X[0];
                if (offsetY != null) p.CenterY[s] = offsetY(x);
                if (offsetZ != null) p.CenterZ[s] = offsetZ(x);
                if (twistRad != null) p.Twist[s] = twistRad(x);
            }
        }

        private static void Set(double[] ring, int stationBase, int j, double y, double z)
        {
            ring[stationBase + j * 2] = y;
            ring[stationBase + j * 2 + 1] = z;
        }
    }
}
