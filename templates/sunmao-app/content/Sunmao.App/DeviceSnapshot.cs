namespace Sunmao.App;

/// <summary>Immutable state projected by the simulated Component.</summary>
/// <param name="Sequence">Number of completed updates.</param>
/// <param name="Value">Current simulated value.</param>
/// <param name="UpdatedAt">Time supplied by the Component's TimeProvider.</param>
public sealed record DeviceSnapshot(long Sequence, int Value, DateTimeOffset UpdatedAt);
