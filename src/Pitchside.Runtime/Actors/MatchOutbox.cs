using Akka.Event;
using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Actors;

/// <summary>
/// One match's ordered side effects, owned by the supervisor so it outlives any one incarnation of the match actor.
/// Writes run one at a time in the order the actor issued them and each is retried until it succeeds, so the store
/// always holds a gapless prefix of the events; a restarted actor waits for <see cref="WritesDrained"/> before it
/// rebuilds. Pushes run one at a time in order too, so a restart cannot overtake sends still in flight.
/// </summary>
public sealed class MatchOutbox : IDisposable
{
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);

    private readonly string _matchId;
    private readonly IMatchStore _store;
    private readonly DurableProgress _progress;
    private readonly ILoggingAdapter _log;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _lock = new();
    private Task _writes = Task.CompletedTask;
    private Task _pushes = Task.CompletedTask;

    public MatchOutbox(string matchId, IMatchStore store, DurableProgress progress, ILoggingAdapter log)
    {
        _matchId = matchId;
        _store = store;
        _progress = progress;
        _log = log;
    }

    /// <summary>Completes when every write issued so far is in the store.</summary>
    public Task WritesDrained
    {
        get
        {
            lock (_lock)
            {
                return _writes;
            }
        }
    }

    public void AppendEvent(MatchEvent matchEvent) => Write("event", async ct =>
    {
        await _store.AppendEventAsync(matchEvent, ct).ConfigureAwait(false);
        _progress.Advance(_matchId, matchEvent.Seq);
    });

    /// <summary>Records events already in the store, found on rebuild, as durable.</summary>
    public void MarkDurable(int seq) => _progress.Advance(_matchId, seq);

    public void Write(string what, Func<CancellationToken, Task> write)
    {
        lock (_lock)
        {
            _writes = WriteAfter(_writes, what, write);
        }
    }

    public void Push(Func<CancellationToken, Task> send)
    {
        lock (_lock)
        {
            _pushes = PushAfter(_pushes, send);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task WriteAfter(Task previous, string what, Func<CancellationToken, Task> write)
    {
        await previous.ConfigureAwait(false);
        var token = _stop.Token;
        var delay = FirstRetryDelay;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await write(token).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
            {
                _log.Warning(ex, "Match {0}: {1} write failed; retrying in {2} ms", _matchId, what, (int)delay.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }

            if (!await DelayAsync(delay, token).ConfigureAwait(false))
            {
                return;
            }

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
        }
    }

    private async Task PushAfter(Task previous, Func<CancellationToken, Task> send)
    {
        await previous.ConfigureAwait(false);
        try
        {
            await send(_stop.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A missed push is not retried: the next snapshot or status tick brings the watcher up to date.
            _log.Warning(ex, "Match {0}: a push to watchers failed", _matchId);
        }
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
