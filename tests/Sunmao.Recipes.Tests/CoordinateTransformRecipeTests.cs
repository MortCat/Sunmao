using Sunmao.Numerics;

namespace Sunmao.Recipes.Tests;

public sealed class CoordinateTransformRecipeTests
{
    [Fact]
    public void MapsThroughTwoCoordinateFramesAndRecoversInput()
    {
        var aFromB = new RigidTransform3d(
            Rotation3d.FromAxisAngle(Vector3d.UnitZ, Math.PI / 2), new Vector3d(10, 0, 0));
        var bFromC = new RigidTransform3d(Rotation3d.Identity, new Vector3d(2, 0, 0));
        var point = CoordinateTransformExample.MapPoint(Vector3d.UnitX, aFromB, bFromC);
        Assert.InRange((point - new Vector3d(10, 3, 0)).Length, 0, 1e-12);
        var recovered = (aFromB * bFromC).Inverse().TransformPoint(point);
        Assert.InRange((recovered - Vector3d.UnitX).Length, 0, 1e-12);
    }
}
