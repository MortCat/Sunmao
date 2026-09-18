using System.Windows;
using System.Windows.Controls;

namespace Sunmao.Wpf.Theme.Controls;

/// <summary>
/// Connection indicator for the chrome bar: a dot and a short label, for example "Devices online".
/// </summary>
/// <remarks>
/// Keep it visible on every page when a connection decides what the application can do, so users
/// never have to open another page to find out.
/// </remarks>
public class ConnectionPill : Control
{
    /// <summary>Identifies the <see cref="Text"/> property.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ConnectionPill), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="IsOnline"/> property.</summary>
    public static readonly DependencyProperty IsOnlineProperty = DependencyProperty.Register(
        nameof(IsOnline), typeof(bool), typeof(ConnectionPill), new PropertyMetadata(false));

    static ConnectionPill()
    {
        // Lookless: the template lives in Themes/Generic.xaml, so apps can re-template it and
        // name elements they place inside its content slots.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ConnectionPill), new FrameworkPropertyMetadata(typeof(ConnectionPill)));
    }

    /// <summary>Label text.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>True shows the online colour; false shows the danger colour.</summary>
    public bool IsOnline
    {
        get => (bool)GetValue(IsOnlineProperty);
        set => SetValue(IsOnlineProperty, value);
    }
}
