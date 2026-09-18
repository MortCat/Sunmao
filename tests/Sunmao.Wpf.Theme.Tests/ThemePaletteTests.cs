using System.Windows;
using System.Windows.Media;
using Sunmao.Testing;
using static Sunmao.Wpf.Theme.Tests.ThemeTestSupport;

namespace Sunmao.Wpf.Theme.Tests;

public sealed class ThemePaletteTests
{
    private static readonly (string Foreground, string Background, double Minimum)[] ContrastPairs =
    [
        ("TextPrimaryBrush", "SurfaceBrush", 4.5), ("TextPrimaryBrush", "BackgroundBrush", 4.5), ("TextPrimaryBrush", "SurfaceMutedBrush", 4.5),
        ("TextSecondaryBrush", "SurfaceBrush", 4.5), ("TextSecondaryBrush", "BackgroundBrush", 4.5), ("TextSecondaryBrush", "SurfaceMutedBrush", 4.5),
        ("TextTertiaryBrush", "SurfaceBrush", 3.0), ("TextTertiaryBrush", "BackgroundBrush", 3.0),
        ("TextOnAccentBrush", "AccentBrush", 4.5), ("AccentBrush", "SurfaceBrush", 3.0), ("AccentBrush", "AccentSoftBrush", 3.0),
        ("TextOnChromeBrush", "ChromeBrush", 4.5), ("TextOnChromeMutedBrush", "ChromeBrush", 4.5),
        ("TextOnChromeSelectedBrush", "ChromeSelectedBrush", 4.5),
        ("SuccessTextBrush", "SuccessSoftBrush", 4.5), ("WarningTextBrush", "WarningSoftBrush", 4.5),
        ("DangerTextBrush", "DangerSoftBrush", 4.5), ("InfoTextBrush", "InfoSoftBrush", 4.5),
        ("SuccessTextBrush", "SurfaceBrush", 4.5), ("WarningTextBrush", "SurfaceBrush", 4.5),
        ("DangerTextBrush", "SurfaceBrush", 4.5), ("InfoTextBrush", "SurfaceBrush", 4.5),
        ("FocusBrush", "SurfaceBrush", 3.0), ("FocusBrush", "BackgroundBrush", 3.0)
    ];

    [Fact]
    public Task EveryVariantDefinesTheSamePaletteKeys() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var keySets = ConcreteVariants
                .Select(variant => PaletteKeys(new SunmaoTheme { Variant = variant }))
                .ToArray();

            Assert.NotEmpty(keySets[0]);
            Assert.All(keySets, keys => Assert.Equal(keySets[0].Order(), keys.Order()));
            return Task.CompletedTask;
        });

    [Theory]
    [InlineData(ThemeVariant.Light)]
    [InlineData(ThemeVariant.Dark)]
    public Task TextAndStatusColoursMeetWcagContrast(ThemeVariant variant) =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme { Variant = variant };
            var failures = ContrastPairs
                .Select(pair => (pair, ratio: ThemePalettes.ContrastRatio(ColorOf(theme, pair.Foreground), ColorOf(theme, pair.Background))))
                .Where(result => result.ratio < result.pair.Minimum)
                .Select(result => $"{result.pair.Foreground} on {result.pair.Background}: {result.ratio:0.00} < {result.pair.Minimum}")
                .ToArray();

            Assert.Empty(failures);
            return Task.CompletedTask;
        });

    [Fact]
    public Task DensitiesDefineTheSameKeysAndTouchTargetsAreAtLeast44Pixels() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var standard = new SunmaoTheme { Density = ThemeDensity.Standard };
            var touch = new SunmaoTheme { Density = ThemeDensity.Touch };

            Assert.Equal(DensityKeys(standard).Order(), DensityKeys(touch).Order());
            foreach (var key in new[] { "ControlHeight", "InputHeight", "IconButtonSize", "RowMinHeight", "SegmentHeight" })
            {
                Assert.True((double)touch[key] >= 44, $"{key} is {touch[key]} in Touch density");
                Assert.True((double)touch[key] > (double)standard[key], $"{key} does not grow in Touch density");
            }

            return Task.CompletedTask;
        });

    [Theory]
    [InlineData("#D32F2F", false)]
    [InlineData("#1976D2", false)]
    [InlineData("#FBC02D", false)]
    [InlineData("#FBC02D", true)]
    [InlineData("#7E57C2", true)]
    public Task DerivedAccentKeepsReadableTextOnTheAccent(string accentHex, bool dark) =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var accent = ThemePalettes.CreateAccent(Hex(accentHex), dark);
            var palette = PaletteKeys(new SunmaoTheme());

            Assert.Subset(palette, accent.Keys.Cast<string>().ToHashSet());
            var ratio = ThemePalettes.ContrastRatio(
                BrushColor(accent["TextOnAccentBrush"]),
                BrushColor(accent["AccentBrush"]));
            Assert.True(ratio >= 4.5, $"text on {accentHex} has contrast {ratio:0.00}");
            return Task.CompletedTask;
        });

    [Fact]
    public void ContrastRatioMatchesTheWcagReferenceValues()
    {
        Assert.Equal(21, ThemePalettes.ContrastRatio(Colors.Black, Colors.White), precision: 2);
        Assert.Equal(1, ThemePalettes.ContrastRatio(Colors.Gray, Colors.Gray), precision: 2);
    }

    private static HashSet<string> PaletteKeys(SunmaoTheme theme) =>
        theme.MergedDictionaries[0].Keys.Cast<object>().Select(key => key.ToString()!).ToHashSet();

    private static HashSet<string> DensityKeys(SunmaoTheme theme) =>
        theme.MergedDictionaries[2].Keys.Cast<object>().Select(key => key.ToString()!).ToHashSet();

    private static Color ColorOf(ResourceDictionary theme, string key) => BrushColor(theme[key]);
}
