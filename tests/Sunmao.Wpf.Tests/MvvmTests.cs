using System.Windows.Threading;
using Sunmao.Testing;
using Sunmao.Wpf.Mvvm;
using Sunmao.Wpf.Threading;

namespace Sunmao.Wpf.Tests;

/// <summary>UiDispatcher is process-wide state, so these tests never run in parallel with others.</summary>
[CollectionDefinition(nameof(UiDispatcherCollection), DisableParallelization = true)]
public sealed class UiDispatcherCollection;

[Collection(nameof(UiDispatcherCollection))]
public sealed class MvvmTests
{
    [Fact]
    public void SetPropertyRaisesOnlyForChangedValues()
    {
        var model = new Probe();
        var raised = new List<string?>();
        model.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        Assert.True(model.SetName("a"));
        Assert.False(model.SetName("a"));
        Assert.True(model.SetName("b"));

        Assert.Equal([nameof(Probe.Name), nameof(Probe.Name)], raised);
    }

    [Fact]
    public void RelayCommandsHonourCanExecuteAndParameterType()
    {
        var runs = 0;
        var enabled = false;
        var command = new RelayCommand(() => runs++, () => enabled);
        var typed = new RelayCommand<string>(value => runs += value.Length);

        command.Execute(null);
        enabled = true;
        command.Execute(null);
        typed.Execute("abc");
        typed.Execute(42);

        Assert.Equal(4, runs);
        Assert.False(typed.CanExecute(42));
    }

    [Fact]
    public Task PropertyChangesOffTheRegisteredUiThreadAreRejected() =>
        SingleThreadUiTestHost.RunAsync(async () =>
        {
            UiDispatcher.Initialize(Dispatcher.CurrentDispatcher);
            try
            {
                var model = new Probe();
                model.SetName("on the UI thread");

                var failure = await Task.Run(() => Record.Exception(() => model.SetName("from a worker")));

                Assert.IsType<InvalidOperationException>(failure);
                Assert.True(UiDispatcher.IsOnUiThread);
            }
            finally
            {
                UiDispatcher.Reset();
            }
        });

    private sealed class Probe : ViewModelBase
    {
        private string _name = string.Empty;

        public string Name => _name;

        public bool SetName(string value) => SetProperty(ref _name, value, nameof(Name));
    }
}
