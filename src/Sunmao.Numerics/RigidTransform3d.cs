namespace Sunmao.Numerics;

/// <summary>An immutable rigid coordinate transform: rotation followed by translation.</summary>
/// <remarks>
/// Thread-safe pure operations, with no lifecycle or cancellation. The default value is identity.
/// Uses column-vector semantics: pOutput = Rotation.Rotate(pInput) + Translation.
/// All positions and translations must share one caller-selected length unit. Coordinate-frame
/// names are not stored or checked. No scale, shear or reflection is represented.
/// Arithmetic throws <see cref="ArithmeticException"/> when a calculation becomes non-finite.
/// Equality is exact; compare transformed points or rotation angle with an appropriate tolerance.
/// </remarks>
public readonly record struct RigidTransform3d
{
    /// <summary>Creates a transform from a normalized rotation and finite translation.</summary>
    /// <param name="rotation">Input-to-output rotation.</param>
    /// <param name="translation">Input origin expressed in output coordinates.</param>
    public RigidTransform3d(Rotation3d rotation, Vector3d translation)
    {
        Rotation = rotation;
        Translation = translation;
    }

    /// <summary>Input-to-output rotation.</summary>
    public Rotation3d Rotation { get; }
    /// <summary>Input origin in output coordinates, using the same units as transformed points.</summary>
    public Vector3d Translation { get; }
    /// <summary>The identity transform, also the default value.</summary>
    public static RigidTransform3d Identity => default;

    /// <summary>Transforms a point, applying both rotation and translation.</summary>
    /// <param name="point">Position in input coordinates.</param>
    public Vector3d TransformPoint(Vector3d point) => Rotation.Rotate(point) + Translation;

    /// <summary>Transforms a direction using only rotation; translation does not apply.</summary>
    /// <param name="direction">Direction in input coordinates; it need not have unit length.</param>
    public Vector3d TransformDirection(Vector3d direction) => Rotation.Rotate(direction);

    /// <summary>Returns the output-to-input transform.</summary>
    public RigidTransform3d Inverse()
    {
        var inverse = Rotation.Inverse();
        return new RigidTransform3d(inverse, inverse.Rotate(-Translation));
    }

    /// <summary>Composes transforms, applying right first and then left.</summary>
    /// <param name="left">Outer transform, for example aFromB.</param>
    /// <param name="right">Inner transform, for example bFromC; the result is aFromC.</param>
    public static RigidTransform3d operator *(RigidTransform3d left, RigidTransform3d right) =>
        new(left.Rotation * right.Rotation, left.Rotation.Rotate(right.Translation) + left.Translation);
}
