using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// One line of a precondition list shown before an expensive or irreversible action: what was
/// checked, the measured value, and a result marker that is a shape as well as a colour.
/// </summary>
/// <remarks>
/// Put the measured value in <see cref="Detail"/> ("128 GB free"), not a restatement of the title;
/// a list that only ever says "fine" teaches people to skip it.
/// </remarks>
public class CheckRow : Control
{
    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(CheckRow), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(CheckRow), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Detail"/> property.</summary>
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(CheckRow), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="State"/> property.</summary>
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(StatusKind), typeof(CheckRow), new PropertyMetadata(StatusKind.Neutral));

    static CheckRow()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CheckRow), new FrameworkPropertyMetadata(typeof(CheckRow)));
    }

    /// <summary>Icon of the neutral badge: what kind of thing is checked.</summary>
    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>What is checked.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The measured value; collapses when empty.</summary>
    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>The result; <see cref="StatusKind.Neutral"/> shows no marker.</summary>
    public StatusKind State
    {
        get => (StatusKind)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }
}
