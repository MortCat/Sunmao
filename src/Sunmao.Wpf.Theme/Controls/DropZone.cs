using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// Large dashed target that says "drop a file or folder here", with a primary action, an optional
/// secondary action, a hint line and a standing note.
/// </summary>
/// <remarks>
/// Presentation only: it does not handle drag and drop. The hosting page sets <c>AllowDrop</c> and
/// owns the drop handler, because what a dropped item means differs per page.
/// </remarks>
public class DropZone : Control
{
    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(DropZone), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DropZone), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Description"/> property.</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(DropZone), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Hint"/> property.</summary>
    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
        nameof(Hint), typeof(string), typeof(DropZone), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Note"/> property.</summary>
    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note), typeof(string), typeof(DropZone), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Action"/> property.</summary>
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(
        nameof(Action), typeof(object), typeof(DropZone), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="SecondaryAction"/> property.</summary>
    public static readonly DependencyProperty SecondaryActionProperty = DependencyProperty.Register(
        nameof(SecondaryAction), typeof(object), typeof(DropZone), new PropertyMetadata(null));

    static DropZone()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DropZone), new FrameworkPropertyMetadata(typeof(DropZone)));
    }

    /// <summary>Large icon at the top.</summary>
    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>What to drop.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>What happens after the drop; wraps.</summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Accepted formats or naming rules; collapses when empty.</summary>
    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>Standing reassurance strip along the bottom; collapses when empty.</summary>
    public string Note
    {
        get => (string)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>The main action, usually a button that opens a picker.</summary>
    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    /// <summary>A quieter alternative below it, usually a link.</summary>
    public object? SecondaryAction
    {
        get => GetValue(SecondaryActionProperty);
        set => SetValue(SecondaryActionProperty, value);
    }
}
