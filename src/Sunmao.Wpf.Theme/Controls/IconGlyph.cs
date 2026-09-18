using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// Renders one of the stroked 24x24 <c>Icon*</c> geometries at any size.
/// </summary>
/// <remarks>
/// <code>&lt;sm:IconGlyph Icon="{StaticResource IconSearch}" Size="18" /&gt;</code>
/// The stroke uses <see cref="Control.Foreground"/>, which is inherited, so the icon takes the colour of
/// the button or text around it.
/// </remarks>
public class IconGlyph : Control
{
    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(IconGlyph), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="Size"/> property.</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(IconGlyph), new PropertyMetadata(18d));

    /// <summary>Identifies the <see cref="Thickness"/> property.</summary>
    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(IconGlyph), new PropertyMetadata(1.6d));

    static IconGlyph()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(IconGlyph), new FrameworkPropertyMetadata(typeof(IconGlyph)));
    }

    /// <summary>One of the <c>Icon*</c> geometries from the theme.</summary>
    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Rendered edge length in device-independent pixels.</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Stroke width. Not scaled with <see cref="Size"/>, which keeps small icons crisp.</summary>
    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }
}
