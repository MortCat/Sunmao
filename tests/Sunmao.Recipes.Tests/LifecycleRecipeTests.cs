using Sunmao.Core.Lifecycle;
using Sunmao.Recipes;

namespace Sunmao.Recipes.Tests;

public sealed class LifecycleRecipeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompositionOrdersCleanupEvenAfterStartFailure(bool fail)
    {
        var calls = new List<string>();
        var first = new Component("source", calls);
        var second = new Component("consumer", calls, fail);
        if (fail)
            await Assert.ThrowsAsync<IOException>(() => LifecycleExample.RunAsync(first, second, CancellationToken.None));
        else
            await LifecycleExample.RunAsync(first, second, CancellationToken.None);

        Assert.Equal(["source.initialize", "consumer.initialize", "source.start", "consumer.start"],
            calls.Take(4));
        Assert.True(calls.IndexOf("consumer.stop") < calls.IndexOf("source.stop"));
        Assert.Equal(["consumer.dispose", "source.dispose"], calls.TakeLast(2));
    }

    private sealed class Component(string name, List<string> calls, bool fail = false) : AppComponentBase(true)
    {
        protected override Task OnInitializeAsync(CancellationToken token)
        {
            calls.Add(name + ".initialize");
            return Task.CompletedTask;
        }
        protected override Task OnStartAsync(CancellationToken token)
        {
            calls.Add(name + ".start");
            return fail ? Task.FromException(new IOException("Injected failure.")) : Task.CompletedTask;
        }
        protected override Task OnStopAsync(CancellationToken token)
        {
            calls.Add(name + ".stop");
            return Task.CompletedTask;
        }
        protected override ValueTask OnDisposeAsync()
        {
            calls.Add(name + ".dispose");
            return ValueTask.CompletedTask;
        }
    }
}
