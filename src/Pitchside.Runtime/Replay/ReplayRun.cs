using Pitchside.Contracts;

namespace Pitchside.Runtime.Replay;

/// <summary>
/// One running replay: its cancellation, its pause gate, the task publishing its events, how far it has published,
/// and how far it has been told to run ahead without waiting (a jump to the next moment).
/// </summary>
public sealed class ReplayRun(string matchId, CancellationTokenSource cancellation) : IDisposable
{
    private readonly Lock _lock = new();
    private bool _disposed;
    private int _published;
    private int _fastForwardTo = -1;
    private IReadOnlyList<MatchEvent> _events = [];
    private IReadOnlyList<Moment> _moments = [];
    private TaskCompletionSource _wake = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string MatchId { get; } = matchId;

    public CancellationTokenSource Cancellation { get; } = cancellation;

    public PauseGate Gate { get; } = new();

    public Task Completion { get; set; } = Task.CompletedTask;

    /// <summary>The events being replayed, in order; empty until the match is stored and the run loaded.</summary>
    public IReadOnlyList<MatchEvent> Events => Volatile.Read(ref _events);

    /// <summary>The moments those events produce, for jumping to the next one.</summary>
    public IReadOnlyList<Moment> Moments => Volatile.Read(ref _moments);

    /// <summary>Gives the run its events and moments before it publishes; the moments first, so events imply both.</summary>
    public void Load(IReadOnlyList<MatchEvent> events, IReadOnlyList<Moment> moments)
    {
        Volatile.Write(ref _moments, moments);
        Volatile.Write(ref _events, events);
    }

    /// <summary>How many events have been published.</summary>
    public int Published => Volatile.Read(ref _published);

    public void MarkPublished(int count) => Volatile.Write(ref _published, count);

    /// <summary>
    /// Publishes every event up to <paramref name="index"/> without waiting between them, and wakes the wait in
    /// progress so it can check whether the jump reaches it. A jump never moves the replay backwards.
    /// </summary>
    public void FastForward(int index)
    {
        lock (_lock)
        {
            _fastForwardTo = Math.Max(_fastForwardTo, index);
            _wake.TrySetResult();
        }
    }

    /// <summary>Whether the event at <paramref name="index"/> is published without waiting for its moment in time.</summary>
    public bool Skips(int index)
    {
        lock (_lock)
        {
            return index <= _fastForwardTo;
        }
    }

    /// <summary>
    /// Waits <paramref name="gap"/> before the event at <paramref name="index"/>, unless a jump reaches it first. A jump
    /// that stops short of it wakes the wait, which then goes on for the time that is left.
    /// </summary>
    public async Task DelayAsync(int index, TimeSpan gap, TimeProvider time, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(time);
        var due = time.GetUtcNow() + gap;
        while (true)
        {
            Task wake;
            lock (_lock)
            {
                if (index <= _fastForwardTo)
                {
                    return;
                }

                if (_wake.Task.IsCompleted)
                {
                    _wake = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                wake = _wake.Task;
            }

            var remaining = due - time.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            using var delay = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timer = Task.Delay(remaining, time, delay.Token);
            var first = await Task.WhenAny(timer, wake).ConfigureAwait(false);
            await delay.CancelAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (first == timer)
            {
                return;
            }
        }
    }

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
