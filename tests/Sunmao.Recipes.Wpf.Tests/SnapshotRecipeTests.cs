using Sunmao.Testing;
using Sunmao.Wpf.Polling;
using System.Windows.Controls;
using System.Windows.Data;

namespace Sunmao.Recipes.Wpf.Tests;

public sealed class SnapshotRecipeTests
{
    [Fact]
    public async Task ProjectionUsesWholeSnapshotOnlyWhileActive()
    {
        await SingleThreadUiTestHost.RunAsync(() =>
        {
            using var pulse = new ManualUiPulseDriver();
            var source = new SnapshotSource();
            using var projection = new SnapshotExample(source, pulse);
            var text = new TextBlock();
            BindingOperations.SetBinding(text, TextBlock.TextProperty,
                new Binding("Value.Value") { Source = projection, Mode = BindingMode.OneWay });
            Assert.Equal("0", text.Text);
            pulse.Start();
            source.Publish(new ValueSnapshot(1, 42));
            pulse.Pulse();
            Assert.Equal(0, projection.Value.Sequence);
            projection.Activate();
            pulse.Pulse();
            Assert.Same(source.Snapshot, projection.Value);
            Assert.Equal("42", text.Text);
            projection.Deactivate();
            source.Publish(new ValueSnapshot(2, 99));
            pulse.Pulse();
            Assert.Equal(1, projection.Value.Sequence);
            Assert.Equal("42", text.Text);
            projection.Dispose();
            pulse.Pulse();
            Assert.Equal(1, projection.Value.Sequence);
            return Task.CompletedTask;
        });
    }
}
