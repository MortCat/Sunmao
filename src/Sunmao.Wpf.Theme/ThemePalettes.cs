using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Sunmao.Wpf.Theme;

/// <summary>Palettes built in code: a custom accent and the Windows high contrast colours.</summary>
internal static class ThemePalettes
{
    private static readonly Color Black = Color.FromRgb(0x1A, 0x12, 0x0A);
    private static readonly Color White = Colors.White;
    private static readonly Color DarkSurface = Color.FromRgb(0x1F, 0x1B, 0x16);

    /// <summary>Accent keys derived from one colour; overrides the palette's accent keys.</summary>
    public static ResourceDictionary CreateAccent(Color accent, bool dark)
    {
        var hover = dark ? Mix(accent, White, 0.15) : Mix(accent, Colors.Black, 0.2);
        var pressed = dark ? Mix(accent, Colors.Black, 0.15) : Mix(accent, Colors.Black, 0.35);
        var soft = dark ? Mix(accent, DarkSurface, 0.82) : Mix(accent, White, 0.9);
        var secondary = Mix(accent, White, 0.3);
        var focus = dark ? Mix(accent, White, 0.4) : accent;
        var onAccent = ContrastRatio(accent, White) >= ContrastRatio(accent, Black) ? White : Black;

        var dictionary = new ResourceDictionary { ["AccentColor"] = accent };
        Add(dictionary, "AccentBrush", accent);
        Add(dictionary, "AccentHoverBrush", hover);
        Add(dictionary, "AccentPressedBrush", pressed);
        Add(dictionary, "AccentSoftBrush", soft);
        Add(dictionary, "AccentSecondaryBrush", secondary);
        Add(dictionary, "TextOnAccentBrush", onAccent);
        Add(dictionary, "FocusBrush", focus);
        if (!dark)
        {
            // The light palette shows the accent on the white selected chrome tab.
            Add(dictionary, "TextOnChromeSelectedBrush", ContrastRatio(accent, White) >= 4.5 ? accent : Color.FromRgb(0x22, 0x1E, 0x19));
        }

        return dictionary;
    }

    /// <summary>
    /// Maps every palette key to the current Windows system colours. Rebuilt whenever the system
    /// colours change, because system brushes are captured by value.
    /// </summary>
    public static ResourceDictionary CreateHighContrast()
    {
        var window = SystemColors.WindowColor;
        var text = SystemColors.WindowTextColor;
        var gray = SystemColors.GrayTextColor;
        var highlight = SystemColors.HighlightColor;
        var highlightText = SystemColors.HighlightTextColor;
        var hotTrack = SystemColors.HotTrackColor;

        var dictionary = new ResourceDictionary { ["AccentColor"] = highlight };
        foreach (var key in new[] { "BackgroundBrush", "SurfaceBrush", "SurfaceSoftBrush", "SurfaceMutedBrush", "SurfaceHoverBrush", "AccentSoftBrush" })
        {
            Add(dictionary, key, window);
        }

        foreach (var key in new[] { "LineBrush", "LineStrongBrush", "TextPrimaryBrush", "TextSecondaryBrush", "DangerLineBrush" })
        {
            Add(dictionary, key, text);
        }

        Add(dictionary, "TextTertiaryBrush", gray);
        Add(dictionary, "AccentBrush", highlight);
        Add(dictionary, "AccentHoverBrush", highlight);
        Add(dictionary, "AccentPressedBrush", highlight);
        Add(dictionary, "AccentSecondaryBrush", hotTrack);
        Add(dictionary, "TextOnAccentBrush", highlightText);
        Add(dictionary, "FocusBrush", text);

        Add(dictionary, "ChromeBrush", window);
        Add(dictionary, "ChromeHoverBrush", highlight);
        Add(dictionary, "ChromeDividerBrush", text);
        Add(dictionary, "ChromeRecessBrush", window);
        Add(dictionary, "TextOnChromeBrush", text);
        Add(dictionary, "TextOnChromeMutedBrush", text);
        Add(dictionary, "OnlineBrush", highlight);
        Add(dictionary, "ChromeSelectedBrush", highlight);
        Add(dictionary, "TextOnChromeSelectedBrush", highlightText);

        foreach (var status in new[] { "Success", "Warning", "Danger", "Info" })
        {
            Add(dictionary, $"{status}Brush", text);
            Add(dictionary, $"{status}SoftBrush", window);
            Add(dictionary, $"{status}TextBrush", text);
        }

        // No shadows in high contrast: borders carry the structure instead.
        dictionary["SoftShadow"] = Frozen(new DropShadowEffect { Opacity = 0 });
        dictionary["LiftShadow"] = Frozen(new DropShadowEffect { Opacity = 0 });
        return dictionary;
    }

    /// <summary>WCAG 2.x contrast ratio between two opaque colours, from 1 to 21.</summary>
    public static double ContrastRatio(Color first, Color second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255d;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    private static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
        (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
        (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
        (byte)Math.Round(from.B + ((to.B - from.B) * amount)));

    private static void Add(ResourceDictionary dictionary, string key, Color color) =>
        dictionary[key] = Frozen(new SolidColorBrush(color));

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
