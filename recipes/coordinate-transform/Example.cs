using Sunmao.Numerics;

namespace Sunmao.Recipes;

internal static class CoordinateTransformExample
{
    // The caller chooses one length unit and supplies matching coordinate frames.
    internal static Vector3d MapPoint(Vector3d pointInC, RigidTransform3d aFromB, RigidTransform3d bFromC)
        => (aFromB * bFromC).TransformPoint(pointInC);
}
