using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Moments;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Bus;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Replay;

/// <summary>
/// Simulates a seed, stores the document, then publishes the events to the bus with the real gaps between their
/// wall times divided by the speed. At most <c>Replay:MaxConcurrent</c> at once, admitted before any work is done
/// and released if the start fails; a match id is replayed once per store. Pause, resume and stop, and a publish
/// that fails, are sent to the match actor so its status, the store and watchers agree. Stopping the host stops them.
/// </summary>
public sealed partial class ReplayService(
    IEventBus bus,
    IMatchStore store,
    ActorRegistry actors,
    IOptions<ReplayOptions> options,
    TimeProvider time,
    ILogger<ReplayService> logger) : IReplayService, IHostedService
{
    private readonly ConcurrentDictionary<string, ReplayRun> _runs = new(StringComparer.Ordinal);
    private readonly Lock _startLock = new();

    /// <summary>How far ahead of the next moment a jump lands, in match time.</summary>
    public static readonly TimeSpan JumpLead = TimeSpan.FromSeconds(10);

    // Kept after a run ends: an instant replay publishes far faster than its match actor consumes, so the match is
    // still being played from the bus after its run is gone. One entry per replay, and a match id replays once.
    private readonly ConcurrentDictionary<string, bool> _instant = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Running => [.. _runs.Keys];

    public bool IsInstant(string matchId) => _instant.TryGetValue(matchId, out var instant) && instant;

    public Task<ReplayStarted> StartAsync(long seed, double speed, CancellationToken cancellationToken) =>
        StartAsync(seed, speed, MatchLoops.First, cancellationToken);

    public async Task<ReplayStarted> StartAsync(long seed, double speed, int loop, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seed);
        ArgumentOutOfRangeException.ThrowIfNegative(speed);
        var matchId = MatchLoops.MatchIdFor(seed, loop);

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

        try
        {
            var match = MatchLoops.As(MatchSimulator.Simulate(seed), matchId);
            var stored = new StoredMatch(match.Document, MatchStatus.Scheduled, 0, time.GetUtcNow());
            if (!await store.TryCreateMatchAsync(stored, cancellationToken).ConfigureAwait(false))
            {
                throw new ReplayConflictException($"{matchId} is already stored; a match id is replayed once per store.");
            }

            // The run learns its events and moments before it publishes, so a jump can find the next moment.
            run.Load(match.Events, MatchMoments.Detect(match.Document, match.Events));
            _instant[matchId] = speed == 0;
            run.Completion = Task.Run(() => PublishAsync(run, match.Events, speed), CancellationToken.None);
            LogStarted(matchId, seed, speed);
            return new ReplayStarted(matchId, seed, speed, match.Events.Count);
        }
        catch
        {
            _runs.TryRemove(matchId, out _);
            run.Dispose();
            throw;
        }
    }

    public bool Stop(string matchId)
    {
        if (!_runs.TryGetValue(matchId, out var run))
        {
            return false;
        }

        run.Cancel();
        Tell(new SetLifecycle(matchId, MatchStatus.Stopped));
        return true;
    }

    public bool Pause(string matchId)
    {
        if (!_runs.TryGetValue(matchId, out var run))
        {
            return false;
        }

        run.Gate.Pause();
        Tell(new SetLifecycle(matchId, MatchStatus.Paused));
        return true;
    }

    public bool Resume(string matchId)
    {
        if (!_runs.TryGetValue(matchId, out var run))
        {
            return false;
        }

        run.Gate.Resume();
        Tell(new SetLifecycle(matchId, MatchStatus.Live));
        return true;
    }

    public bool TryJump(string matchId, out ReplayJump? jump)
    {
        jump = null;
        if (!_runs.TryGetValue(matchId, out var run) || run.Events is not { Count: > 0 } events)
        {
            return false;
        }

        var published = run.Published;
        var lastSeq = published > 0 ? events[published - 1].Seq : 0;
        var next = run.Moments.FirstOrDefault(m => m.TriggerSeq > lastSeq);
        if (next is null)
        {
            return true;
        }

        // The last event at least ten seconds of match time before the moment's trigger, and never behind what is out.
        var trigger = events.First(e => e.Seq == next.TriggerSeq);
        var target = Math.Max(published - 1, 0);
        for (var i = target; i < events.Count && events[i].Seq < next.TriggerSeq; i++)
        {
            if (events[i].WallTimeUtc <= trigger.WallTimeUtc - JumpLead)
            {
                target = i;
            }
        }

        run.FastForward(target);

        // A jump plays on from where it lands, so a paused replay is resumed rather than left waiting at its gate.
        if (run.Gate.IsPaused)
        {
            run.Gate.Resume();
            Tell(new SetLifecycle(matchId, MatchStatus.Live));
        }

        jump = new ReplayJump(matchId, next.Id, next.Type, next.Headline, events[target].Seq);
        LogJumped(matchId, next.Id, events[target].Seq);
        return true;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var runs = _runs.Values.ToList();
        foreach (var run in runs)
        {
            run.Cancel();
        }

        await Task.WhenAll(runs.Select(r => r.Completion)).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishAsync(ReplayRun run, IReadOnlyList<MatchEvent> events, double speed)
    {
        var token = run.Cancellation.Token;
        var next = 0;
        try
        {
            for (var i = 0; i < events.Count; i++)
            {
                next = i;
                await run.Gate.WaitAsync(token).ConfigureAwait(false);
                await bus.PublishAsync(EnvelopeFactory.Wrap(events[i], time.GetUtcNow()), token).ConfigureAwait(false);
                run.MarkPublished(i + 1);
                if (speed > 0 && i + 1 < events.Count && !run.Skips(i + 1))
                {
                    var gap = (events[i + 1].WallTimeUtc - events[i].WallTimeUtc) / speed;
                    if (gap > TimeSpan.Zero)
                    {
                        await run.DelayAsync(i + 1, gap, time, token).ConfigureAwait(false);
                    }
                }
            }

            LogCompleted(run.MatchId, events.Count);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            LogStopped(run.MatchId);
            await MarkStoppedAsync(run.MatchId).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The bus refused an event: the replay cannot continue in order, so it ends as stopped, not half live.
            LogPublishFailed(ex, run.MatchId, events[next].Seq);
            await MarkStoppedAsync(run.MatchId).ConfigureAwait(false);
        }
        finally
        {
            _runs.TryRemove(run.MatchId, out _);
            run.Dispose();
        }
    }

    /// <summary>
    /// The store gets the status directly too, for a replay stopped before its first event created the match actor;
    /// both stores keep a stopped match stopped, so the two writes cannot disagree.
    /// </summary>
    private async Task MarkStoppedAsync(string matchId)
    {
        Tell(new SetLifecycle(matchId, MatchStatus.Stopped));
        await store.UpdateMatchStatusAsync(matchId, MatchStatus.Stopped, 0, CancellationToken.None).ConfigureAwait(false);
    }

    private void Tell(SetLifecycle message)
    {
        if (actors.TryGet<MatchSupervisorActor>(out var matches))
        {
            matches.Tell(message, ActorRefs.NoSender);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Replay {MatchId} could not publish seq {Seq} and was stopped")]
    private partial void LogPublishFailed(Exception exception, string matchId, int seq);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay {MatchId} jumps to seq {Seq}, ahead of moment {MomentId}")]
    private partial void LogJumped(string matchId, string momentId, int seq);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay {MatchId} started from seed {Seed} at speed {Speed}")]
    private partial void LogStarted(string matchId, long seed, double speed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay {MatchId} published all {Events} events")]
    private partial void LogCompleted(string matchId, int events);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay {MatchId} stopped")]
    private partial void LogStopped(string matchId);
}
