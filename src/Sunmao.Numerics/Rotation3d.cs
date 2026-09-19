namespace Sunmao.Numerics;

/// <summary>An immutable right-handed 3D rotation stored as a normalized double-precision quaternion.</summary>
/// <remarks>
/// Thread-safe, pure operations with no lifecycle or cancellation. Angles are radians and rotations
/// are active, acting on column vectors. Quaternion components use (X, Y, Z, W), with W the scalar.
/// The default value is identity. Constructors normalize and choose a canonical quaternion sign.
/// Equality compares canonical components exactly; use <see cref="AngularDistance"/> for tolerance checks.
/// </remarks>
public readonly struct Rotation3d : IEquatable<Rotation3d>
{
    private readonly double _x;
    private readonly double _y;
    private readonly double _z;
    private readonly double _w;
    private readonly bool _initialized;

    private Rotation3d(double x, double y, double z, double w)
    {
        _x = x;
        _y = y;
        _z = z;
        _w = w;
        _initialized = true;
    }

    /// <summary>Quaternion first vector component.</summary>
    public double X => _x;
    /// <summary>Quaternion second vector component.</summary>
    public double Y => _y;
    /// <summary>Quaternion third vector component.</summary>
    public double Z => _z;
    /// <summary>Quaternion scalar component; one for a default value.</summary>
    public double W => _initialized ? _w : 1;
    /// <summary>The identity rotation, also the default value.</summary>
    public static Rotation3d Identity => default;

    /// <summary>Normalizes a finite nonzero quaternion, including very large or subnormal components.</summary>
    /// <param name="x">First vector component.</param>
    /// <param name="y">Second vector component.</param>
    /// <param name="z">Third vector component.</param>
    /// <param name="w">Scalar component.</param>
    /// <exception cref="ArgumentOutOfRangeException">A component is non-finite.</exception>
    /// <exception cref="ArgumentException">All components are zero.</exception>
    public static Rotation3d FromQuaternion(double x, double y, double z, double w)
    {
        Finite.Argument(x, nameof(x));
        Finite.Argument(y, nameof(y));
        Finite.Argument(z, nameof(z));
        Finite.Argument(w, nameof(w));
        var scale = Math.Max(Math.Max(Math.Abs(x), Math.Abs(y)), Math.Max(Math.Abs(z), Math.Abs(w)));
        if (scale == 0)
            throw new ArgumentException("A zero quaternion does not represent a rotation.");
        x /= scale;
        y /= scale;
        z /= scale;
        w /= scale;
        var norm = Math.Sqrt(x * x + y * y + z * z + w * w);
        x /= norm;
        y /= norm;
        z /= norm;
        w /= norm;
        // Choose a hemisphere, resolving the half-turn tie by the first nonzero vector component.
        if (w < 0 || (w == 0 && (x < 0 || (x == 0 && (y < 0 || (y == 0 && z < 0))))))
            return new Rotation3d(-x, -y, -z, -w);
        return new Rotation3d(x, y, z, w);
    }

    /// <summary>Creates an active rotation about a nonzero axis using the right-hand rule.</summary>
    /// <param name="axis">Direction; normalization is automatic.</param>
    /// <param name="angleRadians">Finite angle in radians.</param>
    /// <exception cref="ArgumentOutOfRangeException">The angle is non-finite.</exception>
    /// <exception cref="ArgumentException">The axis is zero, even for a zero angle.</exception>
    public static Rotation3d FromAxisAngle(Vector3d axis, double angleRadians)
    {
        Finite.Argument(angleRadians, nameof(angleRadians));
        if (!axis.TryNormalize(out var unit))
            throw new ArgumentException("The axis must be nonzero.", nameof(axis));
        var half = angleRadians / 2;
        var sine = Math.Sin(half);
        return FromQuaternion(unit.X * sine, unit.Y * sine, unit.Z * sine, Math.Cos(half));
    }

    /// <summary>Rotates a vector without translating it.</summary>
    /// <param name="vector">Vector expressed in the input coordinates.</param>
    /// <exception cref="ArithmeticException">The computed output is outside the finite double range.</exception>
    public Vector3d Rotate(Vector3d vector)
    {
        var scale = vector.Scale;
        if (scale == 0)
            return Vector3d.Zero;
        var scaled = vector / scale;
        var imaginary = new Vector3d(X, Y, Z);
        var cross = Vector3d.Cross(imaginary, scaled);
        return (scaled + (2 * W) * cross + 2 * Vector3d.Cross(imaginary, cross)) * scale;
    }

    /// <summary>Returns the inverse rotation.</summary>
    public Rotation3d Inverse() => FromQuaternion(-X, -Y, -Z, W);

    /// <summary>Returns the shortest angular separation, in radians from zero through pi.</summary>
    /// <param name="other">Rotation to compare with this rotation.</param>
    public double AngularDistance(Rotation3d other)
    {
        var delta = Inverse() * other;
        return 2 * Math.Atan2(new Vector3d(delta.X, delta.Y, delta.Z).Length, Math.Abs(delta.W));
    }

    /// <summary>Composes rotations: (left * right).Rotate(v) applies right first, then left.</summary>
    /// <param name="left">Outer rotation.</param>
    /// <param name="right">Inner rotation.</param>
    public static Rotation3d operator *(Rotation3d left, Rotation3d right) => FromQuaternion(
        left.W * right.X + left.X * right.W + left.Y * right.Z - left.Z * right.Y,
        left.W * right.Y - left.X * right.Z + left.Y * right.W + left.Z * right.X,
        left.W * right.Z + left.X * right.Y - left.Y * right.X + left.Z * right.W,
        left.W * right.W - left.X * right.X - left.Y * right.Y - left.Z * right.Z);

    /// <summary>Compares normalized components exactly; this is not a tolerance-based comparison.</summary>
    /// <param name="other">Rotation to compare.</param>
    public bool Equals(Rotation3d other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Rotation3d other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    /// <summary>Compares normalized components exactly.</summary>
    /// <param name="left">First rotation.</param>
    /// <param name="right">Second rotation.</param>
    public static bool operator ==(Rotation3d left, Rotation3d right) => left.Equals(right);
    /// <summary>Checks for unequal normalized components.</summary>
    /// <param name="left">First rotation.</param>
    /// <param name="right">Second rotation.</param>
    public static bool operator !=(Rotation3d left, Rotation3d right) => !left.Equals(right);
}
