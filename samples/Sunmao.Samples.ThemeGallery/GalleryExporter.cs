using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sunmao.Wpf.Theme;

namespace Sunmao.Samples.ThemeGallery;

/// <summary>Renders the gallery offscreen to PNG files, one per variant and density.</summary>
internal static class GalleryExporter
{
    private const double Width = 1280;

    public static void Export(string folder)
    {
        Directory.CreateDirectory(folder);
        var cases = new (ThemeVariant Variant, ThemeDensity Density)[]
        {
            (ThemeVariant.Light, ThemeDensity.Standard),
            (ThemeVariant.Dark, ThemeDensity.Standard),
            (ThemeVariant.HighContrast, ThemeDensity.Standard),
            (ThemeVariant.Light, ThemeDensity.Touch),
            (ThemeVariant.Dark, ThemeDensity.Touch)
        };

        foreach (var (variant, density) in cases)
        {
            var theme = new SunmaoTheme { Variant = variant, Density = density };
            var view = new GalleryView(theme);
            var host = new Border { Resources = theme, Child = view, Width = Width };
            host.Measure(new Size(Width, double.PositiveInfinity));
            var height = Math.Ceiling(host.DesiredSize.Height);
            host.Arrange(new Rect(0, 0, Width, height));
            host.UpdateLayout();

            var bitmap = new RenderTargetBitmap((int)Width, (int)height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(folder, $"gallery-{variant.ToString().ToLowerInvariant()}-{density.ToString().ToLowerInvariant()}.png");
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}
