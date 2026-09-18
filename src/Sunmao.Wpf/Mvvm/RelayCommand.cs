using System.Windows.Input;

namespace Sunmao.Wpf.Mvvm;

/// <summary>Synchronous <see cref="ICommand"/>.</summary>
/// <remarks>
/// <see cref="RaiseCanExecuteChanged"/> is explicit rather than routed through
/// <c>CommandManager.RequerySuggested</c>, which fires on every keyboard and mouse event and turns
/// into a lot of pointless re-evaluation on pages that refresh from a snapshot many times a second.
/// </remarks>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>Creates the command.</summary>
    /// <param name="execute">Action to run.</param>
    /// <param name="canExecute">Optional availability predicate.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _execute();
        }
    }

    /// <summary>Asks bound controls to re-evaluate <see cref="CanExecute"/>.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Synchronous <see cref="ICommand"/> with a typed parameter.</summary>
/// <typeparam name="T">Parameter type. A parameter of another type makes the command unavailable.</typeparam>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    /// <summary>Creates the command.</summary>
    /// <param name="execute">Action to run.</param>
    /// <param name="canExecute">Optional availability predicate.</param>
    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) =>
        CommandParameter.TryConvert<T>(parameter, out var typed) && (_canExecute?.Invoke(typed) ?? true);

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (CommandParameter.TryConvert<T>(parameter, out var typed) && (_canExecute?.Invoke(typed) ?? true))
        {
            _execute(typed);
        }
    }

    /// <summary>Asks bound controls to re-evaluate <see cref="CanExecute"/>.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal static class CommandParameter
{
    public static bool TryConvert<T>(object? parameter, out T value)
    {
        switch (parameter)
        {
            case T typed:
                value = typed;
                return true;
            case null when default(T) is null:
                value = default!;
                return true;
            default:
                value = default!;
                return false;
        }
    }
}
