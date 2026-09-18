using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Sunmao.Wpf.Theme;

/// <summary>
/// The whole Sunmao look as one resource dictionary. Merge it into <c>App.xaml</c>:
/// <code>
/// &lt;Application.Resources&gt;
///   &lt;ResourceDictionary&gt;
///     &lt;ResourceDictionary.MergedDictionaries&gt;
///       &lt;sm:SunmaoTheme Variant="System" Density="Standard" /&gt;
///     &lt;/ResourceDictionary.MergedDictionaries&gt;
///   &lt;/ResourceDictionary&gt;
/// &lt;/Application.Resources&gt;
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// It holds merged dictionaries in order: the palette for the effective variant, an optional accent
/// override, the density sizes, then the shared foundation, icons and styles. Changing <see cref="Variant"/>,
/// <see cref="Density"/> or <see cref="Accent"/> swaps only the affected dictionary; styles
/// reference palette and density keys through <c>DynamicResource</c>, so a running app restyles
/// immediately.
/// </para>
/// <para>
/// Create and change it on the UI thread. With <see cref="ThemeVariant.System"/> it follows the
/// Windows light/dark and high contrast settings and updates itself on the thread that created it.
/// </para>
/// </remarks>
public sealed class SunmaoTheme : ResourceDictionary
{
    private const string Root = "/Sunmao.Wpf.Theme;component/Themes/";
    private static readonly string[] SharedDictionaries =
    [
        "Shared/Foundation.xaml",
        "Shared/Icons.xaml",
        "Styles/Buttons.xaml",
        "Styles/Inputs.xaml",
        "Styles/Lists.xaml",
        "Styles/Navigation.xaml"
    ];
    private const int PaletteSlot = 0;
    private const int AccentSlot = 1;
    private const int DensitySlot = 2;

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private ThemeVariant _variant = ThemeVariant.Light;
    private ThemeDensity _density = ThemeDensity.Standard;
    private Color? _accent;
    private ThemeVariant? _loadedPalette;
    private ThemeDensity? _loadedDensity;

    /// <summary>Creates the theme with the Light variant and Standard density.</summary>
    public SunmaoTheme()
    {
        MergedDictionaries.Add(new ResourceDictionary());
        MergedDictionaries.Add(new ResourceDictionary());
        MergedDictionaries.Add(new ResourceDictionary());
        foreach (var shared in SharedDictionaries)
        {
            MergedDictionaries.Add(Load(shared));
        }

        Refresh();
    }

    /// <summary>Raised on the UI thread after the palette, accent or density changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Requested colour scheme. <see cref="ThemeVariant.System"/> follows Windows.</summary>
    public ThemeVariant Variant
    {
        get => _variant;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown theme variant.");
            }

            _variant = value;
            if (value == ThemeVariant.System)
            {
                SystemThemeWatcher.Register(this);
            }

            Refresh();
        }
    }

    /// <summary>The palette in use: <see cref="Variant"/> with <see cref="ThemeVariant.System"/> resolved.</summary>
    public ThemeVariant EffectiveVariant { get; private set; }

    /// <summary>Sizing of interactive elements.</summary>
    public ThemeDensity Density
    {
        get => _density;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown theme density.");
            }

            _density = value;
            Refresh();
        }
    }

    /// <summary>
    /// Optional brand colour replacing the palette accent; hover, pressed, soft and focus shades
    /// are derived from it. Ignored in high contrast. Null restores the palette accent.
    /// </summary>
    public Color? Accent
    {
        get => _accent;
        set
        {
            _accent = value;
            Refresh(forceAccent: true);
        }
    }

    /// <summary>Re-resolves <see cref="ThemeVariant.System"/>; called when Windows settings change.</summary>
    internal void RefreshFromSystem()
    {
        if (_variant == ThemeVariant.System)
        {
            // High contrast colours are captured by value, so rebuild them even if still selected.
            _loadedPalette = null;
            Refresh(forceAccent: true);
        }
    }

    internal Dispatcher Dispatcher => _dispatcher;

    private void Refresh(bool forceAccent = false)
    {
        var effective = _variant == ThemeVariant.System ? ThemeManager.ResolveSystemVariant() : _variant;
        var changed = false;

        if (_loadedPalette != effective)
        {
            MergedDictionaries[PaletteSlot] = effective switch
            {
                ThemeVariant.Dark => Load("Palettes/Dark.xaml"),
                ThemeVariant.HighContrast => ThemePalettes.CreateHighContrast(),
                _ => Load("Palettes/Light.xaml")
            };
            _loadedPalette = effective;
            forceAccent = true;
            changed = true;
        }

        if (forceAccent)
        {
            MergedDictionaries[AccentSlot] = _accent is { } accent && effective != ThemeVariant.HighContrast
                ? ThemePalettes.CreateAccent(accent, effective == ThemeVariant.Dark)
                : new ResourceDictionary();
            changed = true;
        }

        if (_loadedDensity != _density)
        {
            MergedDictionaries[DensitySlot] = Load(_density == ThemeDensity.Touch ? "Density/Touch.xaml" : "Density/Standard.xaml");
            _loadedDensity = _density;
            changed = true;
        }

        EffectiveVariant = effective;
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Loads a compiled dictionary from this assembly. LoadComponent reads the resource directly, so
    /// it works without a running Application (tests, headless hosts), unlike a pack:// Source.
    /// Loads are serialized because several UI threads may create themes at once.
    /// </summary>
    private static ResourceDictionary Load(string relativePath) =>
        ComponentLoading.Run(() => (ResourceDictionary)Application.LoadComponent(new Uri(Root + relativePath, UriKind.Relative)));
}
