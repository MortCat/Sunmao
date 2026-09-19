using Sunmao.Core.Lifecycle;

namespace Sunmao.Recipes;

internal static class LifecycleExample
{
    // The composition root owns both components through the coordinator.
    internal static async Task RunAsync(IAppComponent source, IAppComponent consumer, CancellationToken token)
    {
        await using var lifecycle = new ComponentLifecycleCoordinator([source, consumer]);
        await lifecycle.InitializeAsync(token);
        await lifecycle.StartAsync(token);
        await lifecycle.StopAsync(token);
    }
}
