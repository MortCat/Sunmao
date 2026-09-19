namespace Sunmao.Numerics.Tests;

public sealed class RigidTransform3dTests
{
    [Fact]
    public void DefaultAndExplicitIdentityAgree()
    {
        var identity = new RigidTransform3d(Rotation3d.FromQuaternion(0, 0, 0, 1), Vector3d.Zero);
        Assert.Equal(default, identity);
        Assert.Equal(identity.GetHashCode(), RigidTransform3d.Identity.GetHashCode());
        Assert.Equal(new Vector3d(3, 4, 5), identity.TransformPoint(new Vector3d(3, 4, 5)));
    }

    [Fact]
    public void TranslationAppliesToPointsButNotDirections()
    {
        var transform = new RigidTransform3d(
            Rotation3d.FromAxisAngle(Vector3d.UnitZ, Math.PI / 2), new Vector3d(10, 20, 30));
        Rotation3dTests.Near(new Vector3d(10, 21, 30), transform.TransformPoint(Vector3d.UnitX));
        Rotation3dTests.Near(Vector3d.UnitY, transform.TransformDirection(Vector3d.UnitX));
    }

    [Fact]
    public void CompositionHasKnownNoncommutingResult()
    {
        var aFromB = new RigidTransform3d(
            Rotation3d.FromAxisAngle(Vector3d.UnitZ, Math.PI / 2), new Vector3d(10, 0, 0));
        var bFromC = new RigidTransform3d(Rotation3d.Identity, new Vector3d(2, 0, 0));
        Rotation3dTests.Near(new Vector3d(10, 3, 0), (aFromB * bFromC).TransformPoint(Vector3d.UnitX));
        Rotation3dTests.Near(new Vector3d(12, 1, 0), (bFromC * aFromB).TransformPoint(Vector3d.UnitX));
    }

    [Fact]
    public void InverseAndAssociativeCompositionMatchSequentialApplication()
    {
        var a = new RigidTransform3d(Rotation3d.FromAxisAngle(new Vector3d(1, 2, 3), 0.4), new Vector3d(2, -3, 4));
        var b = new RigidTransform3d(Rotation3d.FromAxisAngle(Vector3d.UnitX, -0.7), new Vector3d(-8, 9, 2));
        var c = new RigidTransform3d(Rotation3d.FromAxisAngle(Vector3d.UnitY, 2), new Vector3d(4, 0, -6));
        foreach (var point in new[] { Vector3d.Zero, Vector3d.UnitX, new Vector3d(-20, 3, 9) })
        {
            Rotation3dTests.Near(point, a.Inverse().TransformPoint(a.TransformPoint(point)));
            Rotation3dTests.Near(point, (a.Inverse() * a).TransformPoint(point));
            Rotation3dTests.Near(a.TransformPoint(b.TransformPoint(c.TransformPoint(point))),
                (a * b * c).TransformPoint(point));
            Rotation3dTests.Near(((a * b) * c).TransformPoint(point), (a * (b * c)).TransformPoint(point));
        }
    }

    [Fact]
    public void PositionOverflowIsAnExplicitFailure()
    {
        var value = new Vector3d(double.MaxValue, 0, 0);
        var transform = new RigidTransform3d(Rotation3d.Identity, value);
        Assert.Throws<ArithmeticException>(() => transform.TransformPoint(value));
    }
}
