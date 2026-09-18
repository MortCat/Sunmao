using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sunmao.Wpf.Theme;

namespace Sunmao.Samples.ThemeGallery;

/// <summary>Interactive gallery with live theme switching.</summary>
public partial class MainWindow : Window
{
    private static readonly (string Name, Color? Color)[] Accents =
    [
        ("Default", null),
        ("Violet", Color.FromRgb(0x7E, 0x57, 0xC2)),
        ("Blue", Color.FromRgb(0x19, 0x76, 0xD2)),
        ("Amber", Color.FromRgb(0xF5, 0x9E, 0x0B))
    ];

    private readonly SunmaoTheme _theme = ThemeManager.Current;
    private bool _initializing = true;

    /// <summary>Creates the window with pickers set to the current theme.</summary>
    public MainWindow()
    {
        InitializeComponent();
        VariantPicker.ItemsSource = Enum.GetValues<ThemeVariant>();
        VariantPicker.SelectedItem = _theme.Variant;
        DensityPicker.ItemsSource = Enum.GetValues<ThemeDensity>();
        DensityPicker.SelectedItem = _theme.Density;
        AccentPicker.ItemsSource = Accents.Select(accent => accent.Name).ToArray();
        AccentPicker.SelectedIndex = 0;
        Scroller.Content = new GalleryView(_theme);
        _initializing = false;
    }

    private void OnOptionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _theme.Variant = (ThemeVariant)VariantPicker.SelectedItem;
        _theme.Density = (ThemeDensity)DensityPicker.SelectedItem;
        _theme.Accent = Accents[Math.Max(0, AccentPicker.SelectedIndex)].Color;
    }
}
