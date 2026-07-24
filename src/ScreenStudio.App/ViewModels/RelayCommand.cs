using System.Windows.Input;

namespace ScreenStudio.App.ViewModels;

/// <summary>Minimal ICommand implementation for VM commands. WinUI doesn't ship a
/// RelayCommand, so we provide a tiny one (same pattern as MVVM Toolkit's, minus the
/// dependency). RaiseCanExecuteChanged re-evaluates IsEnabled on bound buttons.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Static hook for command invalidation. WinUI has no global CommandManager like WPF,
/// so this is a no-op shim that lets VMs call it without ifdef-ing. Each RelayCommand must be
/// invalidated individually via RaiseCanExecuteChanged(); this exists only so RecordingViewModel
/// compiles against the WPF-style call site. Replace with explicit per-command invalidation.</summary>
public static class CommandManager
{
    public static void InvalidateRequerySuggested() { /* no-op for WinUI; see summary */ }
}
