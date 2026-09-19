namespace Sunmao.Numerics;

/// <summary>An immutable three-dimensional vector with finite double-precision components.</summary>
/// <remarks>
/// Pure, thread-safe value operations; no lifecycle or cancellation is required. The default value
/// is zero. Units are caller-defined and must be consistent. Arithmetic throws
/// <see cref="ArithmeticException"/> if a calculation produces a non-finite result.
/// Equality is exact component equality; use an application-specific tolerance for measured values.
/// </remarks>
public readonly record struct Vector3d
{
    /// <summary>Creates a vector. Non-finite components throw <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <param name="x">First component.</param>
    /// <param name="y">Second component.</param>
    /// <param name="z">Third component.</param>
    public Vector3d(double x, double y, double z)
    {
        X = Finite.Argument(x, nameof(x));
        Y = Finite.Argument(y, nameof(y));
        Z = Finite.Argument(z, nameof(z));
    }

    /// <summary>First component.</summary>
    public double X { get; }
    /// <summary>Second component.</summary>
    public double Y { get; }
    /// <summary>Third component.</summary>
    public double Z { get; }
    /// <summary>The zero vector, also the default value.</summary>
    public static Vector3d Zero => default;
    /// <summary>The positive first coordinate axis.</summary>
    public static Vector3d UnitX => new(1, 0, 0);
    /// <summary>The positive second coordinate axis.</summary>
    public static Vector3d UnitY => new(0, 1, 0);
    /// <summary>The positive third coordinate axis.</summary>
    public static Vector3d UnitZ => new(0, 0, 1);

    internal double Scale => Math.Max(Math.Abs(X), Math.Max(Math.Abs(Y), Math.Abs(Z)));

    /// <summary>
    /// Euclidean length using scaled components; throws <see cref="ArithmeticException"/>
    /// when the length exceeds the finite double range.
    /// </summary>
    public double Length
    {
        get
        {
            var scale = Scale;
            if (scale == 0)
                return 0;
            var x = X / scale;
            var y = Y / scale;
            var z = Z / scale;
            return Finite.Result(scale * Math.Sqrt(x * x + y * y + z * z));
        }
    }

    /// <summary>Returns a unit vector, including for very large or subnormal nonzero inputs.</summary>
    /// <exception cref="InvalidOperationException">This vector is zero.</exception>
    public Vector3d Normalize()
    {
        if (TryNormalize(out var result))
            return result;
        throw new InvalidOperationException("The zero vector has no direction.");
    }

    /// <summary>Returns false and a zero result only when this vector is zero.</summary>
    /// <param name="result">Unit direction on success; zero on failure.</param>
    public bool TryNormalize(out Vector3d result)
    {
        var scale = Scale;
        if (scale == 0)
        {
            result = Zero;
            return false;
        }
        var scaled = this / scale;
        result = scaled / Math.Sqrt(scaled.X * scaled.X + scaled.Y * scaled.Y + scaled.Z * scaled.Z);
        return true;
    }

    /// <summary>Returns the dot product; non-finite intermediate/final results throw <see cref="ArithmeticException"/>.</summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector.</param>
    public static double Dot(Vector3d left, Vector3d right) =>
        Finite.Result(left.X * right.X + left.Y * right.Y + left.Z * right.Z);

    /// <summary>Returns the right-handed cross product.</summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector.</param>
    public static Vector3d Cross(Vector3d left, Vector3d right) => Result(
        left.Y * right.Z - left.Z * right.Y,
        left.Z * right.X - left.X * right.Z,
        left.X * right.Y - left.Y * right.X);

    /// <summary>Adds vectors with consistent units.</summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector.</param>
    public static Vector3d operator +(Vector3d left, Vector3d right) =>
        Result(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    /// <summary>Subtracts vectors with consistent units.</summary>
    /// <param name="left">First vector.</param>
    /// <param name="right">Second vector.</param>
    public static Vector3d operator -(Vector3d left, Vector3d right) =>
        Result(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    /// <summary>Reverses the direction.</summary>
    /// <param name="value">Vector to negate.</param>
    public static Vector3d operator -(Vector3d value) => new(-value.X, -value.Y, -value.Z);

    /// <summary>Scales a vector; a non-finite scalar throws <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <param name="value">Vector to scale.</param>
    /// <param name="scalar">Finite multiplier.</param>
    public static Vector3d operator *(Vector3d value, double scalar)
    {
        Finite.Argument(scalar, nameof(scalar));
        return Result(value.X * scalar, value.Y * scalar, value.Z * scalar);
    }

    /// <summary>Scales a vector; a non-finite scalar throws <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <param name="scalar">Finite multiplier.</param>
    /// <param name="value">Vector to scale.</param>
    public static Vector3d operator *(double scalar, Vector3d value) => value * scalar;

    /// <summary>Divides each component; a zero or non-finite divisor throws <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <param name="value">Vector to divide.</param>
    /// <param name="scalar">Finite, nonzero divisor.</param>
    public static Vector3d operator /(Vector3d value, double scalar)
    {
        Finite.Argument(scalar, nameof(scalar));
        if (scalar == 0)
            throw new ArgumentOutOfRangeException(nameof(scalar), "The divisor must be nonzero.");
        return Result(value.X / scalar, value.Y / scalar, value.Z / scalar);
    }

    /// <summary>Formats only the coordinates using invariant, round-trip numeric formatting.</summary>
    /// <remarks>Does not evaluate Length, which can overflow even for finite coordinates.</remarks>
    public override string ToString() => FormattableString.Invariant($"({X:R}, {Y:R}, {Z:R})");

    private static Vector3d Result(double x, double y, double z) =>
        new(Finite.Result(x), Finite.Result(y), Finite.Result(z));
}
