namespace Pitchside.Runtime.Replay;

/// <summary>An async gate a replay waits on between events: open while playing, closed while paused.</summary>
public sealed class PauseGate
{
    private readonly Lock _lock = new();
    private TaskCompletionSource _open = CompletedSource();

    public bool IsPaused
    {
        get
        {
            lock (_lock)
            {
                return !_open.Task.IsCompleted;
            }
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_open.Task.IsCompleted)
            {
                _open = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            _open.TrySetResult();
        }
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return _open.Task.WaitAsync(cancellationToken);
        }
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
