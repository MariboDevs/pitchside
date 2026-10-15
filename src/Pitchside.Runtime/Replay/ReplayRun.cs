namespace Pitchside.Runtime.Replay;

/// <summary>One running replay: its cancellation, its pause gate and the task publishing its events.</summary>
public sealed class ReplayRun(string matchId, CancellationTokenSource cancellation) : IDisposable
{
    private readonly Lock _lock = new();
    private bool _disposed;

    public string MatchId { get; } = matchId;

    public CancellationTokenSource Cancellation { get; } = cancellation;

    public PauseGate Gate { get; } = new();

    public Task Completion { get; set; } = Task.CompletedTask;

    /// <summary>
    /// Cancels the run unless it has already finished. A stop can race the run's own end, which disposes the token
    /// source; a finished run has nothing left to cancel.
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                Cancellation.Cancel();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            Cancellation.Dispose();
        }
    }
}
