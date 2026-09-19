namespace Sunmao.Numerics.Tests;

public sealed class Rotation3dTests
{
    internal static void Near(Vector3d expected, Vector3d actual, double tolerance = 2e-12)
    {
        Assert.InRange(Math.Abs(expected.X - actual.X), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Z - actual.Z), 0, tolerance);
    }

    [Fact]
    public void DefaultIsIdentityAndQuaternionSignDoesNotChangeRepresentation()
    {
        var explicitIdentity = Rotation3d.FromQuaternion(0, 0, 0, -2);
        Assert.Equal(Rotation3d.Identity, default);
        Assert.Equal(Rotation3d.Identity, explicitIdentity);
        Assert.Equal(Rotation3d.Identity.GetHashCode(), explicitIdentity.GetHashCode());
        Assert.Equal(Rotation3d.FromQuaternion(1, 2, 3, 4), Rotation3d.FromQuaternion(-1, -2, -3, -4));
        Assert.Equal(Rotation3d.FromQuaternion(0, -1, 0, 0), Rotation3d.FromQuaternion(0, 1, 0, 0));
        Near(Vector3d.UnitX, default(Rotation3d).Rotate(Vector3d.UnitX));
    }

    [Fact]
    public void KnownQuarterTurnsUseRightHandedActiveColumnConventions()
    {
        var x = Rotation3d.FromAxisAngle(Vector3d.UnitX, Math.PI / 2);
        var y = Rotation3d.FromAxisAngle(Vector3d.UnitY, Math.PI / 2);
        var z = Rotation3d.FromAxisAngle(Vector3d.UnitZ, Math.PI / 2);
        Near(Vector3d.UnitZ, x.Rotate(Vector3d.UnitY));
        Near(Vector3d.UnitX, y.Rotate(Vector3d.UnitZ));
        Near(Vector3d.UnitY, z.Rotate(Vector3d.UnitX));
        Near(-Vector3d.UnitY, z.Inverse().Rotate(Vector3d.UnitX));
        // Noncommuting known results catch reversed multiplication order.
        Near(Vector3d.UnitZ, (x * z).Rotate(Vector3d.UnitX));
        Near(Vector3d.UnitY, (z * x).Rotate(Vector3d.UnitX));
    }

    [Theory]
    [InlineData(1e300)]
    [InlineData(1e-300)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    public void QuaternionNormalizationHandlesExtremeScales(double scale)
    {
        var rotation = Rotation3d.FromQuaternion(0, 0, scale, scale);
        Near(Vector3d.UnitY, rotation.Rotate(Vector3d.UnitX));
        Assert.InRange(Math.Abs(rotation.Z * rotation.Z + rotation.W * rotation.W - 1), 0, 2e-15);
    }

    [Fact]
    public void KnownObliqueThirdTurnCyclesAllAxes()
    {
        var rotation = Rotation3d.FromAxisAngle(new Vector3d(1, 1, 1), 2 * Math.PI / 3);
        Near(Vector3d.UnitY, rotation.Rotate(Vector3d.UnitX));
        Near(Vector3d.UnitZ, rotation.Rotate(Vector3d.UnitY));
        Near(Vector3d.UnitX, rotation.Rotate(Vector3d.UnitZ));
        Near(new Vector3d(1, 1, 1), rotation.Rotate(new Vector3d(1, 1, 1)));
    }

    [Fact]
    public void UnrepresentableRotatedCoordinateFailsExplicitly()
    {
        var rotation = Rotation3d.FromAxisAngle(Vector3d.UnitZ, -Math.PI / 4);
        Assert.Throws<ArithmeticException>(() => rotation.Rotate(new Vector3d(double.MaxValue, double.MaxValue, 0)));
    }

    [Theory]
    [InlineData(1e-12)]
    [InlineData(0.1)]
    [InlineData(1.5)]
    [InlineData(3.141592653589793)]
    public void AngularDistanceHandlesSmallAnglesAndHalfTurns(double angle)
    {
        var rotation = Rotation3d.FromAxisAngle(new Vector3d(1, 2, 3), angle);
        Assert.InRange(Math.Abs(Rotation3d.Identity.AngularDistance(rotation) - angle), 0, 2e-15);
        Assert.InRange(rotation.AngularDistance(rotation), 0, 2e-15);
    }

    [Fact]
    public void DeterministicGridPreservesNormAndInverse()
    {
        foreach (var axis in new[] { Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ, new Vector3d(2, -3, 4) })
        foreach (var angle in new[] { -2.7, -0.3, 0.0, 0.6, Math.PI })
        {
            var rotation = Rotation3d.FromAxisAngle(axis, angle);
            var point = new Vector3d(3, -7, 11);
            Near(point, rotation.Inverse().Rotate(rotation.Rotate(point)));
            Assert.InRange(Math.Abs(point.Length - rotation.Rotate(point).Length), 0, 2e-12);
        }
    }

    [Fact]
    public void RepeatedCompositionMaintainsUnitQuaternion()
    {
        var step = Rotation3d.FromAxisAngle(Vector3d.UnitZ, 2 * Math.PI / 1000);
        var accumulated = Rotation3d.Identity;
        for (var i = 0; i < 1000; i++)
            accumulated *= step;
        Near(Vector3d.UnitX, accumulated.Rotate(Vector3d.UnitX));
        Assert.InRange(accumulated.AngularDistance(Rotation3d.Identity), 0, 2e-12);
    }

    [Fact]
    public void RotationKeepsFiniteExtremeIdentityInputs()
    {
        var large = new Vector3d(double.MaxValue, 0, 0);
        Assert.Equal(large, Rotation3d.Identity.Rotate(large));
        var tiny = new Vector3d(double.Epsilon, 0, 0);
        Assert.Equal(tiny, Rotation3d.Identity.Rotate(tiny));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteQuaternionAndAngleAreRejected(double invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rotation3d.FromQuaternion(invalid, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rotation3d.FromQuaternion(0, invalid, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rotation3d.FromQuaternion(0, 0, invalid, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rotation3d.FromQuaternion(0, 0, 0, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rotation3d.FromAxisAngle(Vector3d.UnitX, invalid));
    }

    [Fact]
    public void DegenerateInputsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Rotation3d.FromQuaternion(0, 0, 0, 0));
        Assert.Throws<ArgumentException>(() => Rotation3d.FromAxisAngle(Vector3d.Zero, 0));
    }
}
