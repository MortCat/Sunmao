using Sunmao.Wpf.Mvvm;

namespace Sunmao.Wpf.Tests;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task CancelAndWaitAsyncWaitsForCancelledInvocationCleanup()
    {
        var started = NewSignal();
        var cancellationObserved = NewSignal();
        var allowCleanup = NewSignal();
        var command = new AsyncRelayCommand(async cancellationToken =>
        {
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancellationObserved.TrySetResult();
                await allowCleanup.Task;
                throw;
            }
        });

        var execution = command.ExecuteAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var drain = command.CancelAndWaitAsync();
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(command.IsExecuting);
        Assert.False(drain.IsCompleted);

        allowCleanup.TrySetResult();
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
        await execution.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(command.IsExecuting);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task ConcurrentReentryStartsOnlyOneUntypedInvocation()
    {
        const int attemptCount = 32;
        var startAttempts = NewSignal();
        using var allAttempted = new CountdownEvent(attemptCount);
        var allowCompletion = NewSignal();
        var invocationCount = 0;
        var command = new AsyncRelayCommand(async _ =>
        {
            Interlocked.Increment(ref invocationCount);
            await allowCompletion.Task;
        });

        var attempts = Enumerable.Range(0, attemptCount)
            .Select(_ => Task.Run(async () =>
            {
                await startAttempts.Task;
                var execution = command.ExecuteAsync();
                allAttempted.Signal();
                await execution;
            }))
            .ToArray();

        startAttempts.TrySetResult();
        Assert.True(allAttempted.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, Volatile.Read(ref invocationCount));

        allowCompletion.TrySetResult();
        await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task TypedCommandDrainsTheCapturedRunWithoutCancellingTheNextRun()
    {
        var firstStarted = NewSignal();
        var firstCleanup = NewSignal();
        var secondStarted = NewSignal();
        var secondRelease = NewSignal();
        var secondWasCancelled = false;
        var command = new AsyncRelayCommand<int>(async (value, cancellationToken) =>
        {
            if (value == 1)
            {
                firstStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    firstCleanup.TrySetResult();
                    throw;
                }

                return;
            }

            secondStarted.TrySetResult();
            await secondRelease.Task;
            secondWasCancelled = cancellationToken.IsCancellationRequested;
        });

        var first = command.ExecuteAsync(1);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await command.CancelAndWaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await firstCleanup.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        var second = command.ExecuteAsync(2);
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        secondRelease.TrySetResult();
        await second.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(secondWasCancelled);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task ConcurrentReentryStartsOnlyOneTypedInvocation()
    {
        const int attemptCount = 32;
        var startAttempts = NewSignal();
        using var allAttempted = new CountdownEvent(attemptCount);
        var allowCompletion = NewSignal();
        var values = new List<int>();
        var command = new AsyncRelayCommand<int>(async (value, _) =>
        {
            lock (values)
            {
                values.Add(value);
            }

            await allowCompletion.Task;
        });

        var attempts = Enumerable.Range(0, attemptCount)
            .Select(value => Task.Run(async () =>
            {
                await startAttempts.Task;
                var execution = command.ExecuteAsync(value);
                allAttempted.Signal();
                await execution;
            }))
            .ToArray();

        startAttempts.TrySetResult();
        Assert.True(allAttempted.Wait(TimeSpan.FromSeconds(5)));
        Assert.Single(values);

        allowCompletion.TrySetResult();
        await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task ThrowingCancellationCallbackIsReportedButDrainStillCompletes()
    {
        var started = NewSignal();
        var errors = new List<Exception>();
        var command = new AsyncRelayCommand(async cancellationToken =>
        {
            // Register the delay first: cancellation callbacks run in reverse registration order, so
            // the failing callback runs before the delay continuation leaves this scope.
            var delay = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            using var registration = cancellationToken.Register(
                () => throw new InvalidOperationException("callback failed"));
            started.TrySetResult();
            await delay;
        }, onError: errors.Add);

        var execution = command.ExecuteAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await command.CancelAndWaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await execution.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(command.IsExecuting);
        Assert.Contains(errors, error =>
            error is AggregateException aggregate &&
            aggregate.InnerExceptions.Any(inner => inner.Message == "callback failed"));
    }

    [Fact]
    public async Task CancellationRacingNaturalCompletionIsSafeAndCommandIsReusable()
    {
        TaskCompletionSource started = null!;
        TaskCompletionSource allowCompletion = null!;
        var command = new AsyncRelayCommand(async cancellationToken =>
        {
            started.TrySetResult();
            await allowCompletion.Task.WaitAsync(cancellationToken);
        });

        for (var iteration = 0; iteration < 250; iteration++)
        {
            started = NewSignal();
            allowCompletion = NewSignal();
            var execution = command.ExecuteAsync();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var complete = Task.Run(allowCompletion.TrySetResult);
            var cancelAndDrain = Task.Run(command.CancelAndWaitAsync);
            await Task.WhenAll(complete, cancelAndDrain).WaitAsync(TimeSpan.FromSeconds(5));
            await execution.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(command.IsExecuting);
            Assert.True(command.CanExecute(null));
        }
    }

    [Fact]
    public async Task OwnerDrainWaitsForFinalCommandStateNotification()
    {
        var started = NewSignal();
        var allowExecution = NewSignal();
        var finalNotificationEntered = NewSignal();
        var allowFinalNotification = NewSignal();
        var command = new AsyncRelayCommand(async _ =>
        {
            started.TrySetResult();
            await allowExecution.Task;
        });
        command.CanExecuteChanged += (_, _) =>
        {
            if (!command.IsExecuting)
            {
                finalNotificationEntered.TrySetResult();
                allowFinalNotification.Task.GetAwaiter().GetResult();
            }
        };

        var execution = command.ExecuteAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var drain = command.CancelAndWaitAsync();
        allowExecution.TrySetResult();
        await finalNotificationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(drain.IsCompleted);

        allowFinalNotification.TrySetResult();
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
        await execution.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FailuresGoToTheErrorHandlerAndUnavailableCommandsDoNotRun()
    {
        var errors = new List<Exception>();
        var failing = new AsyncRelayCommand(_ => throw new InvalidOperationException("boom"), onError: errors.Add);
        var runs = 0;
        var unavailable = new AsyncRelayCommand(_ =>
        {
            runs++;
            return Task.CompletedTask;
        }, canExecute: () => false);

        await failing.ExecuteAsync();
        await unavailable.ExecuteAsync();

        Assert.Equal("boom", Assert.Single(errors).Message);
        Assert.Equal(0, runs);
        Assert.False(unavailable.CanExecute(null));
    }

    [Fact]
    public void TypedCommandRejectsParametersOfAnotherType()
    {
        var command = new AsyncRelayCommand<int>((_, _) => Task.CompletedTask);

        Assert.True(command.CanExecute(3));
        Assert.False(command.CanExecute("three"));
        Assert.False(command.CanExecute(null));
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
