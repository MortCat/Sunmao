using System.Windows;
using System.Windows.Controls;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// One number with a label and an optional caption, for dashboards and summary rows.
/// </summary>
/// <remarks>
/// <see cref="Accent"/> tints the number; with <see cref="UseAccentBackground"/> the whole tile takes
/// the status tint. Digits use tabular figures so a changing value never shifts its neighbours.
/// </remarks>
public class MetricTile : Control
{
    /// <summary>Identifies the <see cref="Label"/> property.</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(MetricTile), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Value"/> property.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(MetricTile), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Caption"/> property.</summary>
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(MetricTile), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Accent"/> property.</summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(StatusKind), typeof(MetricTile), new PropertyMetadata(StatusKind.Neutral));

    /// <summary>Identifies the <see cref="UseAccentBackground"/> property.</summary>
    public static readonly DependencyProperty UseAccentBackgroundProperty = DependencyProperty.Register(
        nameof(UseAccentBackground), typeof(bool), typeof(MetricTile), new PropertyMetadata(false));

    static MetricTile()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MetricTile), new FrameworkPropertyMetadata(typeof(MetricTile)));
    }

    /// <summary>What the number measures.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The formatted number.</summary>
    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Optional line under the number, such as a unit or a comparison; collapses when empty.</summary>
    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>Status colour of the number.</summary>
    public StatusKind Accent
    {
        get => (StatusKind)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>When true, the whole tile takes the status tint.</summary>
    public bool UseAccentBackground
    {
        get => (bool)GetValue(UseAccentBackgroundProperty);
        set => SetValue(UseAccentBackgroundProperty, value);
    }
}
