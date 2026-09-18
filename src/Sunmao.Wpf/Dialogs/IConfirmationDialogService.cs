namespace Sunmao.Wpf.Dialogs;

/// <summary>Visual emphasis of a confirmation dialog.</summary>
public enum ConfirmationSeverity
{
    /// <summary>Ordinary confirmation.</summary>
    Neutral,

    /// <summary>The action has consequences worth a second look.</summary>
    Warning,

    /// <summary>The action is destructive or irreversible.</summary>
    Danger
}

/// <summary>Content and button labels of one confirmation request.</summary>
/// <param name="Title">Dialog title.</param>
/// <param name="Message">What will happen if the user confirms.</param>
/// <param name="ConfirmText">Label of the confirm button; pass localized text.</param>
/// <param name="CancelText">Label of the cancel button; pass localized text.</param>
/// <param name="Severity">Visual emphasis.</param>
public sealed record ConfirmationRequest(
    string Title,
    string Message,
    string ConfirmText = "OK",
    string CancelText = "Cancel",
    ConfirmationSeverity Severity = ConfirmationSeverity.Warning)
{
    /// <summary>Throws when a text is empty or the severity is unknown.</summary>
    /// <exception cref="ArgumentException">A text is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The severity is unknown.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(Message);
        ArgumentException.ThrowIfNullOrWhiteSpace(ConfirmText);
        ArgumentException.ThrowIfNullOrWhiteSpace(CancelText);
        if (!Enum.IsDefined(Severity))
        {
            throw new ArgumentOutOfRangeException(nameof(Severity), Severity, "Unknown confirmation severity.");
        }
    }
}

/// <summary>
/// Confirmation port for view models. Depend on it instead of <c>MessageBox</c> so every destructive
/// action shares one look and one test seam. <c>Sunmao.Wpf.Theme</c> provides the themed dialog.
/// </summary>
public interface IConfirmationDialogService
{
    /// <summary>Shows the request and returns true when the user confirms.</summary>
    /// <param name="request">Content of the dialog.</param>
    /// <param name="cancellationToken">Cancels before the dialog is shown.</param>
    Task<bool> ConfirmAsync(ConfirmationRequest request, CancellationToken cancellationToken = default);
}
