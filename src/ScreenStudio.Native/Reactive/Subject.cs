namespace ScreenStudio.Native;

/// <summary>A minimal IObservable implementation for surfacing native events (capture frames,
/// cursor events, audio buffers) without a dependency on System.Reactive.</summary>
internal sealed class Subject<T> : IObservable<T>, IDisposable
{
    private readonly List<IObserver<T>> _observers = new();
    public IDisposable Subscribe(IObserver<T> observer) { lock (_observers) _observers.Add(observer); return new Unsub(this, observer); }
    public void OnNext(T value) { lock (_observers) { for (int i = 0; i < _observers.Count; i++) _observers[i].OnNext(value); } }
    public void OnError(Exception e) { lock (_observers) { for (int i = 0; i < _observers.Count; i++) _observers[i].OnError(e); } }
    public void OnCompleted() { lock (_observers) { for (int i = 0; i < _observers.Count; i++) _observers[i].OnCompleted(); } }
    public void Dispose() { lock (_observers) _observers.Clear(); }
    private sealed class Unsub(Subject<T> s, IObserver<T> o) : IDisposable
    { public void Dispose() { lock (s._observers) s._observers.Remove(o); } }
}
