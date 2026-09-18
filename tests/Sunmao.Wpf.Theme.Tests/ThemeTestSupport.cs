using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sunmao.Wpf.Theme.Tests;

internal static partial class ThemeTestSupport
{
    public static readonly ThemeVariant[] ConcreteVariants = [ThemeVariant.Light, ThemeVariant.Dark, ThemeVariant.HighContrast];

    /// <summary>Hosts <paramref name="content"/> under a theme and runs layout.</summary>
    public static Border Host(SunmaoTheme theme, UIElement content, double width = 800, double height = 600)
    {
        var host = new Border { Resources = theme, Child = content };
        Layout(host, width, height);
        return host;
    }

    public static void Layout(FrameworkElement element, double width = 800, double height = 600)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    public static Color BrushColor(object? brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    public static Color Hex(string value) => (Color)ColorConverter.ConvertFromString(value);

    /// <summary>The Theme package source folder, found by walking up to the repository root.</summary>
    public static string ThemeSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sunmao.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "src", "Sunmao.Wpf.Theme");
    }

    /// <summary>Every resource key referenced with DynamicResource or StaticResource in the package XAML.</summary>
    public static IReadOnlySet<string> ReferencedResourceKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(ThemeSourceDirectory(), "*.xaml", SearchOption.AllDirectories))
        {
            foreach (Match match in ResourceReference().Matches(File.ReadAllText(file)))
            {
                keys.Add(match.Groups["key"].Value);
            }
        }

        return keys;
    }

    [GeneratedRegex(@"\{(?:Dynamic|Static)Resource\s+(?<key>[A-Za-z][A-Za-z0-9]*)\s*\}")]
    private static partial Regex ResourceReference();
}
