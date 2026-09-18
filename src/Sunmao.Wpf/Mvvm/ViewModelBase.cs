using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sunmao.Wpf.Threading;

namespace Sunmao.Wpf.Mvvm;

/// <summary>
/// Minimal <see cref="INotifyPropertyChanged"/> base for view models. No toolkit and no container,
/// so the wiring stays inspectable end to end.
/// </summary>
/// <remarks>
/// Property changes must happen on the UI thread; <see cref="UiDispatcher.VerifyAccess"/> enforces it
/// once a dispatcher is registered. Project backend state on the shared UI polling pulse instead of
/// marshalling from background threads.
/// </remarks>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Sets <paramref name="storage"/> and raises <see cref="PropertyChanged"/> when the value changes.</summary>
    /// <typeparam name="T">Property type.</typeparam>
    /// <param name="storage">Backing field.</param>
    /// <param name="value">New value.</param>
    /// <param name="propertyName">Property name; supplied by the compiler.</param>
    /// <returns>True when the value changed.</returns>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        UiDispatcher.VerifyAccess(nameof(SetProperty));
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises <see cref="PropertyChanged"/>.</summary>
    /// <param name="propertyName">Property name; supplied by the compiler.</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        UiDispatcher.VerifyAccess(nameof(OnPropertyChanged));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
