namespace Sunmao.Numerics;

internal static class Finite
{
    internal static double Argument(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, "A finite value is required.");
        return value;
    }

    internal static double Result(double value)
    {
        if (!double.IsFinite(value))
            throw new ArithmeticException("The calculation produced a non-finite result.");
        return value;
    }
}
