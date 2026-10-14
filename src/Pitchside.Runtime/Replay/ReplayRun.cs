namespace Pitchside.Runtime.Replay;

/// <summary>One running replay: its cancellation, its pause gate and the task publishing its events.</summary>
public sealed class ReplayRun(string matchId, CancellationTokenSource cancellation) : IDisposable
{
    public string MatchId { get; } = matchId;

    public CancellationTokenSource Cancellation { get; } = cancellation;

    public PauseGate Gate { get; } = new();

    public Task Completion { get; set; } = Task.CompletedTask;

    public void Dispose() => Cancellation.Dispose();
}
