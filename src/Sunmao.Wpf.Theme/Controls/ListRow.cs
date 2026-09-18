using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// A record in a list: thumbnail, title, meta line, optional detail, status chip and chevron.
/// For lists users scan for one row rather than read in full.
/// </summary>
public class ListRow : Control
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ListRow), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Meta"/> property.</summary>
    public static readonly DependencyProperty MetaProperty = DependencyProperty.Register(
        nameof(Meta), typeof(string), typeof(ListRow), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Detail"/> property.</summary>
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(ListRow), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="ChipText"/> property.</summary>
    public static readonly DependencyProperty ChipTextProperty = DependencyProperty.Register(
        nameof(ChipText), typeof(string), typeof(ListRow), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="ChipKind"/> property.</summary>
    public static readonly DependencyProperty ChipKindProperty = DependencyProperty.Register(
        nameof(ChipKind), typeof(StatusKind), typeof(ListRow), new PropertyMetadata(StatusKind.Neutral));

    /// <summary>Identifies the <see cref="Thumbnail"/> property.</summary>
    public static readonly DependencyProperty ThumbnailProperty = DependencyProperty.Register(
        nameof(Thumbnail), typeof(ImageSource), typeof(ListRow), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="ShowChevron"/> property.</summary>
    public static readonly DependencyProperty ShowChevronProperty = DependencyProperty.Register(
        nameof(ShowChevron), typeof(bool), typeof(ListRow), new PropertyMetadata(true));

    static ListRow()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ListRow), new FrameworkPropertyMetadata(typeof(ListRow)));
    }

    /// <summary>Main line.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Timestamp or identifier line, in the quietest text colour.</summary>
    public string Meta
    {
        get => (string)GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    /// <summary>Third line, typically a count; collapses when empty.</summary>
    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>Chip label; empty removes the chip.</summary>
    public string ChipText
    {
        get => (string)GetValue(ChipTextProperty);
        set => SetValue(ChipTextProperty, value);
    }

    /// <summary>Chip status.</summary>
    public StatusKind ChipKind
    {
        get => (StatusKind)GetValue(ChipKindProperty);
        set => SetValue(ChipKindProperty, value);
    }

    /// <summary>Preview image; the well keeps its size when null.</summary>
    public ImageSource? Thumbnail
    {
        get => (ImageSource?)GetValue(ThumbnailProperty);
        set => SetValue(ThumbnailProperty, value);
    }

    /// <summary>Whether the row leads somewhere; false hides the chevron.</summary>
    public bool ShowChevron
    {
        get => (bool)GetValue(ShowChevronProperty);
        set => SetValue(ShowChevronProperty, value);
    }
}
