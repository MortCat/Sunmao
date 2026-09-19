using Sunmao.Testing;
using System.Windows.Controls;
using System.Windows.Data;

namespace Sunmao.Recipes.Wpf.Tests;

public sealed class CommandRecipeTests
{
    [Fact]
    public async Task ClosingCancelsAndWaitsForCleanupBeforeRejectingFurtherWork()
    {
        await SingleThreadUiTestHost.RunAsync(async () =>
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var errors = new List<Exception>();
            await using var owner = new CommandExample(async token =>
            {
                entered.SetResult();
                try { await never.Task.WaitAsync(token); }
                finally
                {
                    cancelled.SetResult();
                    await cleanup.Task;
                }
            }, errors.Add);
            var execution = owner.Command.ExecuteAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var closing = owner.DisposeAsync().AsTask();
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                Assert.False(closing.IsCompleted);
                Assert.False(owner.Command.CanExecute(null));
            }
            finally { cleanup.TrySetResult(); }
            await closing.WaitAsync(TimeSpan.FromSeconds(5));
            await execution;
            await owner.Command.ExecuteAsync();
            Assert.False(owner.Command.IsExecuting);
            Assert.Empty(errors);
        });
    }

    [Fact]
    public async Task OperationFailureIsReportedAndDrained()
    {
        await SingleThreadUiTestHost.RunAsync(async () =>
        {
            var errors = new List<Exception>();
            await using var owner = new CommandExample(
                _ => Task.FromException(new InvalidOperationException("Injected failure.")), errors.Add);
            var button = new Button();
            BindingOperations.SetBinding(button, Button.CommandProperty,
                new Binding("Command") { Source = owner, Mode = BindingMode.OneWay });
            Assert.Same(owner.Command, button.Command);
            await owner.Command.ExecuteAsync();
            Assert.Single(errors);
            Assert.False(owner.Command.IsExecuting);
        });
    }
}
