namespace Sunmao.Wpf.Theme;

/// <summary>Colour scheme of a <see cref="SunmaoTheme"/>.</summary>
public enum ThemeVariant
{
    /// <summary>Light surfaces with dark text.</summary>
    Light,

    /// <summary>Dark surfaces with light text.</summary>
    Dark,

    /// <summary>The Windows high contrast colours, taken from <c>SystemColors</c>.</summary>
    HighContrast,

    /// <summary>
    /// Follows Windows: high contrast when it is on, otherwise the "app mode" light/dark setting.
    /// The theme updates itself when the setting changes.
    /// </summary>
    System
}

/// <summary>Sizing of interactive elements.</summary>
public enum ThemeDensity
{
    /// <summary>Mouse and keyboard at a desk.</summary>
    Standard,

    /// <summary>Touch panels: targets of at least 44 px, larger spacing and scroll bars.</summary>
    Touch
}

/// <summary>
/// Semantic status of a chip, marker or notice. Colours follow from the meaning, never from the
/// call site, so a status looks the same everywhere and survives a palette change.
/// </summary>
public enum StatusKind
{
    /// <summary>No judgement: counts, labels, inert metadata.</summary>
    Neutral,

    /// <summary>Passed, complete, connected.</summary>
    Success,

    /// <summary>Degraded or partially complete; needs attention but is not a failure.</summary>
    Warning,

    /// <summary>Failed, rejected, disconnected.</summary>
    Danger,

    /// <summary>Informational emphasis; in progress.</summary>
    Info
}
