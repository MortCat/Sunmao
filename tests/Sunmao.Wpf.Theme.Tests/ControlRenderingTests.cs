using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sunmao.Testing;
using Sunmao.Wpf.Dialogs;
using Sunmao.Wpf.Theme.Controls;
using Sunmao.Wpf.Theme.Dialogs;
using static Sunmao.Wpf.Theme.Tests.ThemeTestSupport;

namespace Sunmao.Wpf.Theme.Tests;

public sealed class ControlRenderingTests
{
    [Theory]
    [InlineData(ThemeVariant.Light)]
    [InlineData(ThemeVariant.Dark)]
    [InlineData(ThemeVariant.HighContrast)]
    public Task EveryControlLaysOutAndRendersUnderTheTheme(ThemeVariant variant) =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme { Variant = variant };
            var panel = new StackPanel();
            foreach (var control in AllControls())
            {
                panel.Children.Add(control);
            }

            var host = Host(theme, new ScrollViewer { Content = panel }, 900, 2400);

            var bitmap = new RenderTargetBitmap(900, 2400, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            Assert.All(panel.Children.Cast<FrameworkElement>(), child => Assert.True(child.ActualWidth > 0, child.GetType().Name));
            return Task.CompletedTask;
        });

    [Theory]
    [InlineData(StatusKind.Success, "SuccessTextBrush")]
    [InlineData(StatusKind.Warning, "WarningTextBrush")]
    [InlineData(StatusKind.Danger, "DangerTextBrush")]
    [InlineData(StatusKind.Info, "InfoTextBrush")]
    [InlineData(StatusKind.Neutral, "TextSecondaryBrush")]
    public Task StatusChipTextFollowsItsKind(StatusKind kind, string expectedBrush) =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme();
            var chip = new StatusChip { Text = "state", Kind = kind };
            Host(theme, chip);

            var label = (TextBlock)chip.Template.FindName("Label", chip);
            Assert.Equal(BrushColor(theme[expectedBrush]), BrushColor(label.Foreground));
            return Task.CompletedTask;
        });

    [Theory]
    [InlineData(ConfirmationSeverity.Warning, StatusKind.Warning, "PrimaryButton")]
    [InlineData(ConfirmationSeverity.Danger, StatusKind.Danger, "DangerButton")]
    [InlineData(ConfirmationSeverity.Neutral, StatusKind.Info, "PrimaryButton")]
    public Task ConfirmationDialogMapsSeverityToNoticeAndButton(
        ConfirmationSeverity severity,
        StatusKind expectedKind,
        string expectedStyle) =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme();
            var window = new ConfirmationDialogWindow(new ConfirmationRequest("Delete run", "This cannot be undone.", "Delete", "Keep", severity))
            {
                Resources = theme
            };
            try
            {
                window.ApplyTemplate();
                var notice = (NoticeBar)window.FindName("MessageBar");
                var confirm = (Button)window.FindName("ConfirmButton");

                Assert.Equal("Delete run", window.Title);
                Assert.Equal(expectedKind, notice.Kind);
                Assert.Same(theme[expectedStyle], confirm.Style);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });

    [Fact]
    public Task ConsumersCanNameElementsInsideContentSlots() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            // Controls that host caller content must not own a XAML name scope, or x:Name inside
            // Body/Actions fails with MC3093 at compile time in the consuming project.
            const string xaml = """
                <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      xmlns:sm="urn:sunmao-wpf">
                  <StackPanel>
                    <sm:InfoCard Header="Card"><sm:InfoCard.Body><TextBox x:Name="InCard" /></sm:InfoCard.Body></sm:InfoCard>
                    <sm:PageHeader Title="Page"><sm:PageHeader.Actions><Button x:Name="InHeader" /></sm:PageHeader.Actions></sm:PageHeader>
                    <sm:EmptyState Title="Empty"><sm:EmptyState.Action><Button x:Name="InEmpty" /></sm:EmptyState.Action></sm:EmptyState>
                    <sm:DropZone Title="Drop"><sm:DropZone.Action><Button x:Name="InDrop" /></sm:DropZone.Action></sm:DropZone>
                  </StackPanel>
                </Grid>
                """;

            var root = (Grid)System.Windows.Markup.XamlReader.Parse(xaml);
            Host(new SunmaoTheme(), root);

            Assert.IsType<TextBox>(root.FindName("InCard"));
            Assert.IsType<Button>(root.FindName("InHeader"));
            Assert.IsType<Button>(root.FindName("InEmpty"));
            Assert.IsType<Button>(root.FindName("InDrop"));
            return Task.CompletedTask;
        });

    [Fact]
    public Task InvalidConfirmationRequestsAreRejected() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            Assert.Throws<ArgumentException>(() => new ConfirmationDialogWindow(new ConfirmationRequest(" ", "message")));
            return Task.CompletedTask;
        });

    private static IEnumerable<FrameworkElement> AllControls()
    {
        yield return new IconGlyph { Icon = Geometry.Parse("M4.8,12.6 L9.6,17.4 L19.2,6.8") };
        yield return new StatusChip { Text = "Complete", Kind = StatusKind.Success, ShowDot = true };
        yield return new ConnectionPill { Text = "Online", IsOnline = true };
        yield return new InfoCard { Header = "Card", Body = new TextBlock { Text = "Body" } };
        yield return new MetricTile { Label = "Passed", Value = "1,284", Caption = "today", Accent = StatusKind.Success, UseAccentBackground = true };
        yield return new NoticeBar { Text = "A long message that wraps across the available width when there is not enough room.", Kind = StatusKind.Warning };
        yield return new PageHeader { Title = "Title", Subtitle = "Subtitle", Actions = new Button { Content = "Action" } };
        yield return new EmptyState { Title = "Nothing here", Description = "Import something to begin." };
        yield return new ListRow { Title = "Row", Meta = "2026-09-18 10:00", Detail = "12 items", ChipText = "Partial", ChipKind = StatusKind.Warning };
        yield return new CheckRow { Title = "Disk space", Detail = "128 GB free", State = StatusKind.Success };
        yield return new DropZone { Title = "Drop a folder", Description = "Files are copied.", Hint = "PNG or JPEG", Note = "Sources are never modified." };
        yield return StyledButton("GhostButton");
        yield return StyledButton("DangerButton");
        yield return new CheckBox { Content = "Check", IsChecked = true };
        yield return StyledCheckBox("ToggleSwitch");
        yield return new ComboBox { ItemsSource = new[] { "One", "Two" }, SelectedIndex = 0 };
        yield return new TextBox { Text = "Text" };
        yield return new Slider { Value = 4 };
        yield return new ProgressBar { Value = 40 };
        yield return new TabControl { ItemsSource = new[] { new TabItem { Header = "A" }, new TabItem { Header = "B" } } };
        yield return new DataGrid { ItemsSource = new[] { new { Name = "a", Count = 1 } }, Height = 120 };
    }

    private static Button StyledButton(string styleKey)
    {
        var button = new Button { Content = styleKey };
        button.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
        return button;
    }

    private static CheckBox StyledCheckBox(string styleKey)
    {
        var box = new CheckBox { Content = styleKey, IsChecked = true };
        box.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
        return box;
    }
}
