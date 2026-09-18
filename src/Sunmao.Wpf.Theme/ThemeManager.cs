using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Sunmao.Wpf.Theme;

/// <summary>Finds the application's <see cref="SunmaoTheme"/> and reads the Windows theme settings.</summary>
/// <remarks>
/// Typical settings-page code: <c>ThemeManager.Current.Variant = ThemeVariant.Dark;</c>. Call it on
/// the UI thread.
/// </remarks>
public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The theme merged into <c>Application.Current.Resources</c>.</summary>
    /// <exception cref="InvalidOperationException">There is no application or no merged <see cref="SunmaoTheme"/>.</exception>
    public static SunmaoTheme Current =>
        Find(Application.Current?.Resources)
        ?? throw new InvalidOperationException("No SunmaoTheme is merged into Application.Current.Resources.");

    /// <summary>Searches <paramref name="resources"/> and its merged dictionaries, depth first.</summary>
    /// <param name="resources">Dictionary to search; null returns null.</param>
    public static SunmaoTheme? Find(ResourceDictionary? resources)
    {
        if (resources is null)
        {
            return null;
        }

        if (resources is SunmaoTheme theme)
        {
            return theme;
        }

        foreach (var merged in resources.MergedDictionaries)
        {
            if (Find(merged) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// The variant Windows asks for: <see cref="ThemeVariant.HighContrast"/> when high contrast is on,
    /// otherwise the "default app mode" setting. Falls back to Light when the setting is unreadable.
    /// </summary>
    public static ThemeVariant ResolveSystemVariant()
    {
        if (SystemParameters.HighContrast)
        {
            return ThemeVariant.HighContrast;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key?.GetValue("AppsUseLightTheme") is int useLight)
            {
                return useLight == 0 ? ThemeVariant.Dark : ThemeVariant.Light;
            }
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Unreadable policy-locked settings fall back to Light.
        }

        return ThemeVariant.Light;
    }
}

/// <summary>
/// Delivers Windows theme changes to every live <see cref="SunmaoTheme"/> that follows the system,
/// on the thread that created each theme. Holds themes weakly.
/// </summary>
internal static class SystemThemeWatcher
{
    private static readonly object Gate = new();
    private static readonly List<WeakReference<SunmaoTheme>> Themes = [];
    private static bool _subscribed;

    public static void Register(SunmaoTheme theme)
    {
        bool subscribe;
        lock (Gate)
        {
            Themes.RemoveAll(reference => !reference.TryGetTarget(out _));
            if (!Themes.Any(reference => reference.TryGetTarget(out var existing) && ReferenceEquals(existing, theme)))
            {
                Themes.Add(new WeakReference<SunmaoTheme>(theme));
            }

            subscribe = !_subscribed;
            _subscribed = true;
        }

        // SystemEvents may create its own window and thread; keep that outside the lock.
        if (subscribe)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
    }

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs args)
    {
        if (args.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Accessibility or UserPreferenceCategory.Color))
        {
            return;
        }

        SunmaoTheme[] alive;
        lock (Gate)
        {
            alive = Themes
                .Select(reference => reference.TryGetTarget(out var theme) ? theme : null)
                .OfType<SunmaoTheme>()
                .ToArray();
        }

        foreach (var theme in alive)
        {
            if (!theme.Dispatcher.HasShutdownStarted)
            {
                _ = theme.Dispatcher.BeginInvoke(new Action(theme.RefreshFromSystem));
            }
        }
    }
}
