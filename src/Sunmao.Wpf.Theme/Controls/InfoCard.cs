using System.Windows;
using System.Windows.Controls;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// A card with an optional header line (title plus a right-aligned action) and a body.
/// </summary>
/// <remarks>
/// <code>
/// &lt;sm:InfoCard Header="Recent imports"&gt;
///   &lt;sm:InfoCard.HeaderAction&gt;&lt;Button Style="{DynamicResource LinkButton}" Content="See all" /&gt;&lt;/sm:InfoCard.HeaderAction&gt;
///   &lt;sm:InfoCard.Body&gt;…&lt;/sm:InfoCard.Body&gt;
/// &lt;/sm:InfoCard&gt;
/// </code>
/// An empty <see cref="Header"/> removes the header line.
/// </remarks>
public class InfoCard : Control
{
    /// <summary>Identifies the <see cref="Header"/> property.</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(InfoCard), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="HeaderAction"/> property.</summary>
    public static readonly DependencyProperty HeaderActionProperty = DependencyProperty.Register(
        nameof(HeaderAction), typeof(object), typeof(InfoCard), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="Body"/> property.</summary>
    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(object), typeof(InfoCard), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="BodyMargin"/> property.</summary>
    public static readonly DependencyProperty BodyMarginProperty = DependencyProperty.Register(
        nameof(BodyMargin), typeof(Thickness), typeof(InfoCard), new PropertyMetadata(new Thickness(0, 14, 0, 0)));

    static InfoCard()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(InfoCard), new FrameworkPropertyMetadata(typeof(InfoCard)));
    }

    /// <summary>Card title; empty removes the header line.</summary>
    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Optional content at the right of the header, usually a link or icon button.</summary>
    public object? HeaderAction
    {
        get => GetValue(HeaderActionProperty);
        set => SetValue(HeaderActionProperty, value);
    }

    /// <summary>Card content.</summary>
    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    /// <summary>Space between the header and the body.</summary>
    public Thickness BodyMargin
    {
        get => (Thickness)GetValue(BodyMarginProperty);
        set => SetValue(BodyMarginProperty, value);
    }
}
