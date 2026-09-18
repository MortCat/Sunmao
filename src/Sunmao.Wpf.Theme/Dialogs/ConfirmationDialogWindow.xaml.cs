using System.Windows;
using Sunmao.Wpf.Dialogs;

namespace Sunmao.Wpf.Theme.Dialogs;

/// <summary>Themed modal confirmation window used by <see cref="ConfirmationDialogService"/>.</summary>
public partial class ConfirmationDialogWindow : Window
{
    /// <summary>Creates the window for <paramref name="request"/>.</summary>
    /// <param name="request">Content of the dialog; validated.</param>
    public ConfirmationDialogWindow(ConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        ComponentLoading.Run(InitializeComponent);
        Title = request.Title;
        DataContext = request;
        MessageBar.Kind = request.Severity switch
        {
            ConfirmationSeverity.Neutral => StatusKind.Info,
            ConfirmationSeverity.Warning => StatusKind.Warning,
            _ => StatusKind.Danger
        };
        ConfirmButton.SetResourceReference(
            StyleProperty,
            request.Severity == ConfirmationSeverity.Danger ? "DangerButton" : "PrimaryButton");
    }

    private void OnConfirmClicked(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;
}

/// <summary>
/// <see cref="IConfirmationDialogService"/> that shows <see cref="ConfirmationDialogWindow"/> over the
/// application's main window. Call it on the UI thread.
/// </summary>
public sealed class ConfirmationDialogService : IConfirmationDialogService
{
    /// <inheritdoc />
    public Task<bool> ConfirmAsync(ConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        Sunmao.Wpf.Threading.UiDispatcher.VerifyAccess(nameof(ConfirmAsync));

        var dialog = new ConfirmationDialogWindow(request);
        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner != dialog && owner.IsLoaded)
        {
            dialog.Owner = owner;
        }

        // ShowDialog runs WPF's nested modal loop on the UI thread; nothing else blocks on it.
        return Task.FromResult(dialog.ShowDialog() == true);
    }
}
