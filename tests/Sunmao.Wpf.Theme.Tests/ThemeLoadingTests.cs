using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sunmao.Testing;
using static Sunmao.Wpf.Theme.Tests.ThemeTestSupport;

namespace Sunmao.Wpf.Theme.Tests;

public sealed class ThemeLoadingTests
{
    [Fact]
    public Task ThemeLoadsWithoutAnApplicationAndStylesResolve() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme();
            var button = PrimaryButton();
            Host(theme, button);

            Assert.Equal(ThemeVariant.Light, theme.EffectiveVariant);
            Assert.Equal(Hex("#8A5528"), BrushColor(button.Background));
            return Task.CompletedTask;
        });

    [Fact]
    public Task EveryReferencedResourceKeyResolvesInEveryVariantAndDensity() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var referenced = ReferencedResourceKeys();
            Assert.Contains("AccentBrush", referenced);

            foreach (var variant in ConcreteVariants)
            {
                foreach (var density in new[] { ThemeDensity.Standard, ThemeDensity.Touch })
                {
                    var theme = new SunmaoTheme { Variant = variant, Density = density };
                    var missing = referenced.Where(key => !theme.Contains(key)).ToArray();
                    Assert.True(missing.Length == 0, $"{variant}/{density} is missing: {string.Join(", ", missing)}");
                }
            }

            return Task.CompletedTask;
        });

    [Fact]
    public Task ChangingTheVariantRestylesLiveControls() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme();
            var button = PrimaryButton();
            Host(theme, button);

            theme.Variant = ThemeVariant.Dark;
            Layout(button);

            Assert.Equal(ThemeVariant.Dark, theme.EffectiveVariant);
            Assert.Equal(Hex("#D9A46E"), BrushColor(button.Background));
            return Task.CompletedTask;
        });

    [Fact]
    public Task ChangingTheDensityResizesLiveControls() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme();
            var button = PrimaryButton();
            Host(theme, button);
            Assert.Equal(36, button.Height);

            theme.Density = ThemeDensity.Touch;
            Layout(button);

            Assert.Equal(46, button.Height);
            return Task.CompletedTask;
        });

    [Fact]
    public Task AccentOverridesThePaletteUntilCleared() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme();
            var changes = 0;
            theme.Changed += (_, _) => changes++;
            var button = PrimaryButton();
            Host(theme, button);

            theme.Accent = Hex("#7E57C2");
            Assert.Equal(Hex("#7E57C2"), BrushColor(button.Background));

            theme.Variant = ThemeVariant.Dark;
            Assert.Equal(Hex("#7E57C2"), BrushColor(button.Background));

            theme.Accent = null;
            Assert.Equal(Hex("#D9A46E"), BrushColor(button.Background));
            Assert.Equal(3, changes);
            return Task.CompletedTask;
        });

    [Fact]
    public Task SystemVariantResolvesToAConcretePalette() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme { Variant = ThemeVariant.System };

            Assert.Equal(ThemeVariant.System, theme.Variant);
            Assert.Contains(theme.EffectiveVariant, ConcreteVariants);
            Assert.Equal(ThemeManager.ResolveSystemVariant(), theme.EffectiveVariant);
            return Task.CompletedTask;
        });

    [Fact]
    public Task ThemeManagerFindsANestedTheme() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme();
            var outer = new ResourceDictionary();
            var middle = new ResourceDictionary();
            middle.MergedDictionaries.Add(theme);
            outer.MergedDictionaries.Add(middle);

            Assert.Same(theme, ThemeManager.Find(outer));
            Assert.Null(ThemeManager.Find(new ResourceDictionary()));
            return Task.CompletedTask;
        });

    [Fact]
    public Task InvalidOptionsAreRejected() =>
        SingleThreadUiTestHost.RunAsync(() =>
        {
            var theme = new SunmaoTheme();

            Assert.Throws<ArgumentOutOfRangeException>(() => theme.Variant = (ThemeVariant)42);
            Assert.Throws<ArgumentOutOfRangeException>(() => theme.Density = (ThemeDensity)42);
            return Task.CompletedTask;
        });

    private static Button PrimaryButton()
    {
        var button = new Button { Content = "OK" };
        button.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        return button;
    }
}
