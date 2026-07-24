namespace ScreenStudio.Core.Math;

/// <summary>Immutable 2D vector with double precision. All core math is unit-agnostic
/// (screen-space pixels) so it is fully testable without any GPU/window dependencies.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => a * s;
    public static Vec2 operator /(Vec2 a, double s) => new(a.X / s, a.Y / s);

    public double Length => System.Math.Sqrt(X * X + Y * Y);
    public Vec2 Lerp(Vec2 other, double t) => this + (other - this) * t;
    public override string ToString() => $"({X:0.###}, {Y:0.###})";
}
