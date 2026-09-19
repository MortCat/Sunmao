using Sunmao.Wpf.Mvvm;
using Sunmao.Wpf.Polling;

namespace Sunmao.Recipes;

internal sealed record ValueSnapshot(long Sequence, int Value);

internal sealed class SnapshotSource
{
    private ValueSnapshot _snapshot = new(0, 0);
    internal ValueSnapshot Snapshot => Volatile.Read(ref _snapshot);

    // Exactly one backend writer; readers may run on any thread.
    internal void Publish(ValueSnapshot snapshot) => Volatile.Write(ref _snapshot, snapshot);
}

internal sealed class SnapshotExample : ViewModelBase, IDisposable
{
    private readonly IUiPollingSubscription _subscription;
    private ValueSnapshot _value = new(0, 0);
    /// <summary>Latest UI-thread projection; public so WPF bindings can read it.</summary>
    public ValueSnapshot Value => _value;

    internal SnapshotExample(SnapshotSource source, IUiPollingSource pulse)
    {
        _subscription = pulse.Register("snapshot-example", _ =>
            SetProperty(ref _value, source.Snapshot, nameof(Value)));
    }

    internal void Activate() => _subscription.Enable();
    internal void Deactivate() => _subscription.Disable();
    /// <summary>Remove this projection's subscription on the UI thread without stopping the shared pulse.</summary>
    public void Dispose() => _subscription.Dispose();
}
