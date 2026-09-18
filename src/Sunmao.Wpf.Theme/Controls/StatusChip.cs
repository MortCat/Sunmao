using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// A small tinted pill stating one fact, such as "Complete", "Offline" or "3 pending".
/// </summary>
/// <remarks>
/// The tint follows from <see cref="Kind"/>, never from a colour passed in, so the same status looks
/// the same everywhere. Set <see cref="ShowDot"/> when the status must not rely on colour alone.
/// </remarks>
public class StatusChip : Control
{
    /// <summary>Identifies the <see cref="Text"/> property.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(StatusChip), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Kind"/> property.</summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(StatusKind), typeof(StatusChip), new PropertyMetadata(StatusKind.Neutral));

    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(StatusChip), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="ShowDot"/> property.</summary>
    public static readonly DependencyProperty ShowDotProperty = DependencyProperty.Register(
        nameof(ShowDot), typeof(bool), typeof(StatusChip), new PropertyMetadata(false));

    /// <summary>Identifies the <see cref="ShowBackground"/> property.</summary>
    public static readonly DependencyProperty ShowBackgroundProperty = DependencyProperty.Register(
        nameof(ShowBackground), typeof(bool), typeof(StatusChip), new PropertyMetadata(true));

    static StatusChip()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StatusChip), new FrameworkPropertyMetadata(typeof(StatusChip)));
    }

    /// <summary>Label text.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Semantic status; decides the tint.</summary>
    public StatusKind Kind
    {
        get => (StatusKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Optional leading icon; collapses when unset.</summary>
    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Shows a coloured dot before the label so the status does not rely on tint alone.</summary>
    public bool ShowDot
    {
        get => (bool)GetValue(ShowDotProperty);
        set => SetValue(ShowDotProperty, value);
    }

    /// <summary>When false, renders only the dot and text without the pill surface.</summary>
    public bool ShowBackground
    {
        get => (bool)GetValue(ShowBackgroundProperty);
        set => SetValue(ShowBackgroundProperty, value);
    }
}
