namespace Sunmao.Numerics.Tests;

public sealed class Vector3dTests
{
    [Fact]
    public void DefaultAndBasicAlgebraHaveKnownResults()
    {
        Assert.Equal(new Vector3d(0, 0, 0), default);
        Assert.Equal(0, Vector3d.Zero.Length);
        var value = new Vector3d(2, 3, 6);
        Assert.Equal(7, value.Length);
        Assert.Equal(new Vector3d(4, 6, 12), value + value);
        Assert.Equal(Vector3d.Zero, value - value);
        Assert.Equal(new Vector3d(-2, -3, -6), -value);
        Assert.Equal(value, (2 * value) / 2);
        Assert.Equal(49, Vector3d.Dot(value, value));
        Assert.Equal(Vector3d.UnitZ, Vector3d.Cross(Vector3d.UnitX, Vector3d.UnitY));
        Assert.Equal(-Vector3d.UnitZ, Vector3d.Cross(Vector3d.UnitY, Vector3d.UnitX));
    }

    [Theory]
    [InlineData(1e300)]
    [InlineData(1e-300)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    public void NormalizationAvoidsSquaringOverflowAndUnderflow(double scale)
    {
        var vector = new Vector3d(scale, -scale, scale);
        var unit = vector.Normalize();
        Assert.InRange(Math.Abs(unit.Length - 1), 0, 2e-15);
        Assert.InRange(Math.Abs(unit.X - 1 / Math.Sqrt(3)), 0, 2e-15);
        Assert.Equal(-unit.X, unit.Y);
        Assert.Equal(unit.X, unit.Z);
        Assert.True(vector.TryNormalize(out var second));
        Assert.Equal(unit, second);
    }

    [Fact]
    public void TinyNonzeroLengthAndZeroDirectionAreDistinct()
    {
        Assert.Equal(double.Epsilon, new Vector3d(double.Epsilon, 0, 0).Length);
        Assert.False(Vector3d.Zero.TryNormalize(out var result));
        Assert.Equal(Vector3d.Zero, result);
        Assert.Throws<InvalidOperationException>(() => Vector3d.Zero.Normalize());
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RejectsNonFiniteInputs(double invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3d(invalid, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3d(0, invalid, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3d(0, 0, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => Vector3d.UnitX * invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => Vector3d.UnitX / invalid);
    }

    [Fact]
    public void DivisionAndOverflowFailuresAreExplicit()
    {
        var large = new Vector3d(double.MaxValue, 0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => large / 0);
        Assert.Throws<ArithmeticException>(() => large + large);
        Assert.Throws<ArithmeticException>(() => large * 2);
        Assert.Throws<ArithmeticException>(() => Vector3d.Dot(large, large));
        Assert.Throws<ArithmeticException>(() => Vector3d.Cross(large, new Vector3d(0, double.MaxValue, 0)));
        Assert.Throws<ArithmeticException>(() => new Vector3d(double.MaxValue, double.MaxValue, 0).Length);
        Assert.Equal(Vector3d.UnitX, large.Normalize());
    }

    [Fact]
    public void FormattingFiniteCoordinatesDoesNotEvaluateOverflowingLength()
    {
        Assert.Equal("(1.25, -2.5, 0)", new Vector3d(1.25, -2.5, 0).ToString());
        var large = new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue);
        Assert.Contains("1.7976931348623157E+308", large.ToString());
    }
}
