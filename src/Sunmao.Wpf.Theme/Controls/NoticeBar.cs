using System.Windows;
using System.Windows.Controls;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// A full-width message with a status icon, for explanations, warnings and errors inside a page.
/// The text wraps, so long or translated messages stay readable.
/// </summary>
public class NoticeBar : Control
{
    /// <summary>Identifies the <see cref="Text"/> property.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(NoticeBar), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Kind"/> property.</summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(StatusKind), typeof(NoticeBar), new PropertyMetadata(StatusKind.Neutral));

    static NoticeBar()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NoticeBar), new FrameworkPropertyMetadata(typeof(NoticeBar)));
    }

    /// <summary>Message text; wraps.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Status; decides the tint and the icon.</summary>
    public StatusKind Kind
    {
        get => (StatusKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }
}
