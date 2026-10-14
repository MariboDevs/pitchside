using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Runtime.Bus;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Replay;

/// <summary>
/// Simulates a seed, stores the document, then publishes the events to the bus with the real gaps between their
/// wall times divided by the speed. At most <c>Replay:MaxConcurrent</c> at once. Stopping the host stops them.
/// </summary>
public sealed partial class ReplayService(
    IEventBus bus,
    IMatchStore store,
    IOptions<ReplayOptions> options,
    TimeProvider time,
    ILogger<ReplayService> logger) : IReplayService, IHostedService
{
    private readonly ConcurrentDictionary<string, ReplayRun> _runs = new(StringComparer.Ordinal);
    private readonly Lock _startLock = new();

    public IReadOnlyCollection<string> Running => [.. _runs.Keys];

    public async Task<ReplayStarted> StartAsync(long seed, double speed, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seed);
        ArgumentOutOfRangeException.ThrowIfNegative(speed);
        var match = MatchSimulator.Simulate(seed);
        var matchId = match.Document.MatchId;

        if (await store.GetMatchAsync(matchId, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new ReplayConflictException($"{matchId} is already stored; a match id is replayed once per store.");
        }

        ReplayRun run;
        lock (_startLock)
        {
            if (_runs.ContainsKey(matchId))
            {
                throw new ReplayConflictException($"{matchId} is already replaying.");
            }

            if (_runs.Count >= options.Value.MaxConcurrent)
            {
                throw new ReplayLimitReachedException($"{options.Value.MaxConcurrent} replays are already running.");
            }

            run = new ReplayRun(matchId, new CancellationTokenSource());
            _runs[matchId] = run;
        }

        await store.SaveMatchAsync(new StoredMatch(match.Document, MatchStatus.Scheduled, 0, time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        run.Completion = Task.Run(() => PublishAsync(run, match.Events, speed), CancellationToken.None);
        LogStarted(matchId, seed, speed);
        return new ReplayStarted(matchId, seed, speed, match.Events.Count);
    }

    public bool Stop(string matchId)
    {
        if (!_runs.TryGetValue(matchId, out var run))
        {
            return false;
        }

        run.Cancellation.Cancel();
        return true;
    }

    public bool Pause(string matchId)
    {
        if (!_runs.TryGetValue(matchId, out var run))
        {
            return false;
        }

        run.Gate.Pause();
        return true;
    }

    public bool Resume(string matchId)
    {
        if (!_runs.TryGetValue(matchId, out var run))
        {
            return false;
        }

        run.Gate.Resume();
        return true;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var run in _runs.Values)
        {
            await run.Cancellation.CancelAsync().ConfigureAwait(false);
        }

        await Task.WhenAll(_runs.Values.Select(r => r.Completion)).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishAsync(ReplayRun run, IReadOnlyList<MatchEvent> events, double speed)
    {
        var token = run.Cancellation.Token;
        try
        {
            for (var i = 0; i < events.Count; i++)
            {
                await run.Gate.WaitAsync(token).ConfigureAwait(false);
                await bus.PublishAsync(EnvelopeFactory.Wrap(events[i], time.GetUtcNow()), token).ConfigureAwait(false);
                if (speed > 0 && i + 1 < events.Count)
                {
                    var gap = (events[i + 1].WallTimeUtc - events[i].WallTimeUtc) / speed;
                    if (gap > TimeSpan.Zero)
                    {
                        await Task.Delay(gap, time, token).ConfigureAwait(false);
                    }
                }
            }

            LogCompleted(run.MatchId, events.Count);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            LogStopped(run.MatchId);
            await store.UpdateMatchStatusAsync(run.MatchId, MatchStatus.Stopped, 0, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _runs.TryRemove(run.MatchId, out _);
            run.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay {MatchId} started from seed {Seed} at speed {Speed}")]
    private partial void LogStarted(string matchId, long seed, double speed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay {MatchId} published all {Events} events")]
    private partial void LogCompleted(string matchId, int events);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay {MatchId} stopped")]
    private partial void LogStopped(string matchId);
}
