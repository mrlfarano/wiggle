namespace ScreenStudio.App.ViewModels;

/// <summary>Adapter wrapping an Action into an IObserver (WinUI has no built-in
/// Subscribe(Action) overload; the capture observables take IObserver&lt;T&gt;).</summary>
internal sealed class ActionObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    public ActionObserver(Action<T> onNext) => _onNext = onNext;
    public void OnNext(T value) => _onNext(value);
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}
