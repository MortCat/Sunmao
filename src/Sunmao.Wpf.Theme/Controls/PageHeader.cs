using System.Windows;
using System.Windows.Controls;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// Page title line: title and subtitle on the left, page actions on the right, and an optional
/// second row for the page's own secondary navigation.
/// </summary>
public class PageHeader : Control
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Subtitle"/> property.</summary>
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Actions"/> property.</summary>
    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="Secondary"/> property.</summary>
    public static readonly DependencyProperty SecondaryProperty = DependencyProperty.Register(
        nameof(Secondary), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    static PageHeader()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PageHeader), new FrameworkPropertyMetadata(typeof(PageHeader)));
    }

    /// <summary>Page title.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Short description on the title line; trimmed when space runs out.</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Right-aligned actions, usually buttons in a horizontal StackPanel.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>Optional second row, typically a SegmentedHost with SegmentTab buttons.</summary>
    public object? Secondary
    {
        get => GetValue(SecondaryProperty);
        set => SetValue(SecondaryProperty, value);
    }
}
