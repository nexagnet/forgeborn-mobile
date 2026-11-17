using System;

namespace ForgeBorn.Domain.Geometry
{
    /// <summary>2D vector (double) in the blade cross-section plane: X = blade width (Y axis), Y = thickness (Z axis).</summary>
    public readonly struct Vec2d
    {
        public readonly double X;
        public readonly double Y;

        public Vec2d(double x, double y) { X = x; Y = y; }

        public double Length => Math.Sqrt(X * X + Y * Y);
        public static Vec2d operator +(Vec2d a, Vec2d b) => new Vec2d(a.X + b.X, a.Y + b.Y);
        public static Vec2d operator -(Vec2d a, Vec2d b) => new Vec2d(a.X - b.X, a.Y - b.Y);
        public static Vec2d operator *(Vec2d a, double s) => new Vec2d(a.X * s, a.Y * s);
        public static double Dot(Vec2d a, Vec2d b) => a.X * b.X + a.Y * b.Y;
        public static double Cross(Vec2d a, Vec2d b) => a.X * b.Y - a.Y * b.X;

        public Vec2d Normalized()
        {
            double l = Length;
            return l > 1e-300 ? new Vec2d(X / l, Y / l) : new Vec2d(0, 0);
        }

        public bool IsFinite => !(double.IsNaN(X) || double.IsInfinity(X) || double.IsNaN(Y) || double.IsInfinity(Y));
    }

    /// <summary>3D vector (double), blade-local axes: +X length (tang to tip), Y width, Z thickness. SI meters.</summary>
    public readonly struct Vec3d
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3d(double x, double y, double z) { X = x; Y = y; Z = z; }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public static Vec3d operator +(Vec3d a, Vec3d b) => new Vec3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3d operator -(Vec3d a, Vec3d b) => new Vec3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3d operator *(Vec3d a, double s) => new Vec3d(a.X * s, a.Y * s, a.Z * s);

        public static Vec3d Lerp(Vec3d a, Vec3d b, double t) => a + (b - a) * t;

        public Vec3d Normalized()
        {
            double l = Length;
            return l > 1e-300 ? new Vec3d(X / l, Y / l, Z / l) : new Vec3d(0, 0, 0);
        }

        public bool IsFinite =>
            !(double.IsNaN(X) || double.IsInfinity(X) || double.IsNaN(Y) || double.IsInfinity(Y) ||
              double.IsNaN(Z) || double.IsInfinity(Z));
    }
}
