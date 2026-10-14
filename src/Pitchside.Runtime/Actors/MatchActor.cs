using Akka.Actor;
using Akka.Event;
using Pitchside.Contracts;
using Pitchside.Moments;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.MomentSink;
using Pitchside.Runtime.Storage;
using Pitchside.Stats;

namespace Pitchside.Runtime.Actors;

/// <summary>
/// One match, live. The only writer of the match's events, fact sheets and moments. It applies events strictly
/// in sequence (older ones are dropped and counted, a gap is buffered and filled from the store), runs the stats
/// and moment stages, persists without blocking, pushes to watchers in order, and hands moments to the crew.
/// Writes and pushes go through the match's <see cref="MatchOutbox"/>, which keeps them ordered and retries writes,
/// and which outlives a restart. On (re)start the actor waits for earlier writes to land, then rebuilds its state by
/// replaying the stored events.
/// </summary>
public sealed class MatchActor : ReceiveActor, IWithUnboundedStash, IWithTimers
{
    public const int FactSheetEveryEvents = 5;
    public const int MaxLoadAttempts = 40;
    public static readonly TimeSpan LoadRetryDelay = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan GapRetryDelay = TimeSpan.FromSeconds(1);

    private readonly string _matchId;
    private readonly IMatchStore _store;
    private readonly IMatchBroadcaster _broadcaster;
    private readonly IMomentSink _sink;
    private readonly MatchOutbox _outbox;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IMatchStatsEngine _stats = new MatchStatsEngine();
    private readonly IMomentDetector _detector = new MomentDetector();
    private readonly List<Moment> _moments = [];
    private readonly SortedDictionary<int, MatchEvent> _pending;
    private readonly HashSet<string> _subscribers = new(StringComparer.Ordinal);

    private StoredMatch? _match;
    private MatchStatsState? _statsState;
    private DetectorState? _detectorState;
    private FactSheet? _latestSheet;
    private MatchStatus _status;
    private int _lastSeq;
    private int _period;
    private int _clockMs;
    private int _dropped;
    private int _eventsSinceSheet;
    private int _lastSavedMinute = -1;
    private int _loadAttempt;
    private bool _gapRequested;

    public MatchActor(string matchId, IMatchStore store, IMatchBroadcaster broadcaster, IMomentSink sink, MatchOutbox outbox)
    {
        _matchId = matchId;
        _store = store;
        _broadcaster = broadcaster;
        _sink = sink;
        _outbox = outbox;
        _pending = outbox.Pending;
        Become(Loading);
    }

    public IStash Stash { get; set; } = null!;

    public ITimerScheduler Timers { get; set; } = null!;

    public static Props Props(string matchId, IMatchStore store, IMatchBroadcaster broadcaster, IMomentSink sink, MatchOutbox outbox) =>
        Akka.Actor.Props.Create(() => new MatchActor(matchId, store, broadcaster, sink, outbox));

    protected override void PreStart() => Load();

    private void Loading()
    {
        Receive<MatchLoaded>(OnLoaded);
        Receive<RetryLoad>(_ => Load());
        Receive<LoadFailed>(failed => throw new InvalidOperationException($"Could not load match {_matchId}.", failed.Cause));
        ReceiveAny(_ => Stash.Stash());
    }

    private void Live()
    {
        Receive<IngestEvent>(message => Ingest(message.Event));
        Receive<ResendLoaded>(OnResend);
        Receive<ResendFailed>(failed =>
        {
            _gapRequested = false;
            _log.Warning(failed.Cause, "Match {0}: reading the gap from the store failed; retrying", _matchId);
            Timers.StartSingleTimer("gap", RetryGap.Instance, GapRetryDelay);
        });
        Receive<RetryGap>(_ => RequestGap());
        Receive<SetLifecycle>(OnLifecycle);
        Receive<GetState>(_ => Sender.Tell(View()));
        Receive<GetFactSheet>(_ => Sender.Tell(_latestSheet ?? _stats.Snapshot(_statsState!)));
        Receive<GetMoments>(_ => Sender.Tell(_moments.ToList()));
        Receive<GetLiveState>(_ => Sender.Tell(new LiveState(View(), _latestSheet ?? _stats.Snapshot(_statsState!), _moments.ToList())));
        Receive<Subscribe>(message => _subscribers.Add(message.ConnectionId));
        Receive<Unsubscribe>(message => _subscribers.Remove(message.ConnectionId));
        Receive<StatusTick>(_ =>
        {
            var status = View().ToStatus();
            Push(ct => _broadcaster.StatusAsync(status, ct));
        });
    }

    private void Load()
    {
        _loadAttempt++;
        LoadAsync().PipeTo(Self, success: loaded => loaded, failure: ex => new LoadFailed(ex));
    }

    private async Task<MatchLoaded> LoadAsync()
    {
        // Writes from an earlier incarnation may still be landing; the store is a gapless prefix once they have.
        await _outbox.WritesDrained.ConfigureAwait(false);
        var match = await _store.GetMatchAsync(_matchId, CancellationToken.None).ConfigureAwait(false);
        if (match is null)
        {
            return new MatchLoaded(null, [], new HashSet<string>(StringComparer.Ordinal));
        }

        var events = await _store.GetEventsAsync(_matchId, 1, int.MaxValue, CancellationToken.None).ConfigureAwait(false);
        var narratives = events.Count == 0 ? [] : await _store.GetNarrativesAsync(_matchId, CancellationToken.None).ConfigureAwait(false);
        return new MatchLoaded(match, events, narratives.Select(n => n.MomentId).ToHashSet(StringComparer.Ordinal));
    }

    private void OnLoaded(MatchLoaded loaded)
    {
        if (loaded.Match is null)
        {
            // The producer stores the document before it publishes; an event can still outrun it on the bus.
            if (_loadAttempt < MaxLoadAttempts)
            {
                Timers.StartSingleTimer("load", new RetryLoad(_loadAttempt + 1), LoadRetryDelay);
                return;
            }

            _log.Error("Match {0} has no stored document after {1} attempts; stopping", _matchId, _loadAttempt);
            Context.Stop(Self);
            return;
        }

        _match = loaded.Match;
        _status = loaded.Match.Status;
        _statsState = _stats.Start(loaded.Match.Document);
        _detectorState = _detector.Start(loaded.Match.Document);
        var expected = 1;
        var rebuilt = new List<(Moment Moment, FactSheet Sheet)>();
        foreach (var e in loaded.Events)
        {
            if (e.Seq != expected)
            {
                _log.Warning("Match {0}: stored events stop being contiguous at seq {1}; the rest is filled from the bus", _matchId, expected);
                break;
            }

            rebuilt.AddRange(Apply(e, live: false));
            expected++;
        }

        if (_lastSeq > 0)
        {
            Reconcile(rebuilt, loaded.NarratedMomentIds);
            _log.Info("Match {0} rebuilt from stored events, last seq {1}", _matchId, _lastSeq);
        }

        Become(Live);
        Timers.StartPeriodicTimer("status", StatusTick.Instance, StatusInterval);
        ResumePending();
        Stash.UnstashAll();
    }

    /// <summary>
    /// After a rebuild, rewrites what the stored events imply, in case a crash came between an event's append and its
    /// derived writes: the durable watermark, the status, the latest fact sheet, each moment and its fact sheet. All
    /// are idempotent upserts and nothing is broadcast again. Moments without a narrative go back to the crew.
    /// </summary>
    private void Reconcile(List<(Moment Moment, FactSheet Sheet)> rebuilt, IReadOnlySet<string> narrated)
    {
        _outbox.MarkDurable(_lastSeq);
        var status = _status;
        var lastSeq = _lastSeq;
        Persist("match status", ct => _store.UpdateMatchStatusAsync(_matchId, status, lastSeq, ct));
        var latest = _latestSheet!;
        Persist("fact sheet", ct => _store.SaveFactSheetAsync(latest, ct));
        foreach (var (moment, sheet) in rebuilt)
        {
            Persist("fact sheet", ct => _store.SaveFactSheetAsync(sheet, ct));
            Persist("moment", ct => _store.SaveMomentAsync(moment, ct));
            if (!narrated.Contains(moment.Id))
            {
                var item = new MomentWorkItem(moment, sheet, _match!.Document);
                Persist("hand to the crew", ct => _sink.EnqueueAsync(item, ct).AsTask());
            }
        }
    }

    /// <summary>
    /// Events buffered behind a gap by an earlier incarnation are still in the outbox: drop what the rebuild already
    /// covers, apply what now follows on, and ask for any gap that is still open.
    /// </summary>
    private void ResumePending()
    {
        foreach (var seq in _pending.Keys.Where(seq => seq <= _lastSeq).ToList())
        {
            _pending.Remove(seq);
        }

        while (_pending.Remove(_lastSeq + 1, out var next))
        {
            Apply(next, live: true);
        }

        RequestGap();
    }

    private void Ingest(MatchEvent e)
    {
        if (e.Seq <= _lastSeq)
        {
            _dropped++;
            return;
        }

        if (e.Seq > _lastSeq + 1)
        {
            _pending[e.Seq] = e;
            if (!_gapRequested)
            {
                _log.Warning("GapDetected: match {0} expected seq {1}, got {2}; requesting a resend from the store", _matchId, _lastSeq + 1, e.Seq);
                RequestGap();
            }

            return;
        }

        Apply(e, live: true);
        while (_pending.Remove(_lastSeq + 1, out var next))
        {
            Apply(next, live: true);
        }

        if (_pending.Count == 0)
        {
            _gapRequested = false;
        }
    }

    private void RequestGap()
    {
        if (_gapRequested || _pending.Count == 0)
        {
            return;
        }

        _gapRequested = true;
        _store.GetEventsAsync(_matchId, _lastSeq + 1, _pending.Keys.First() - 1, CancellationToken.None)
            .PipeTo(Self, success: events => new ResendLoaded(events), failure: ex => new ResendFailed(ex));
    }

    private void OnResend(ResendLoaded resend)
    {
        _gapRequested = false;
        var before = _lastSeq;
        foreach (var e in resend.Events.OrderBy(e => e.Seq))
        {
            Ingest(e);
        }

        if (_pending.Count == 0 || _gapRequested)
        {
            return;
        }

        // Progress means the next gap is a different one, behind a later buffered event: ask for it now. No progress
        // means the store does not have the events yet: ask again after a pause rather than loop.
        if (_lastSeq > before)
        {
            RequestGap();
        }
        else
        {
            Timers.StartSingleTimer("gap", RetryGap.Instance, GapRetryDelay);
        }
    }

    private void OnLifecycle(SetLifecycle lifecycle)
    {
        var status = MatchStatusTransition.Resolve(_status, lifecycle.Status);
        if (status == _status)
        {
            return;
        }

        _status = status;
        var lastSeq = _lastSeq;
        Persist("match status", ct => _store.UpdateMatchStatusAsync(_matchId, status, lastSeq, ct));
        var view = View().ToStatus();
        Push(ct => _broadcaster.StatusAsync(view, ct));
    }

    /// <summary>Applies one event; returns the moments it raised with the fact sheet they were raised at.</summary>
    private List<(Moment Moment, FactSheet Sheet)> Apply(MatchEvent e, bool live)
    {
        var step = _stats.Apply(_statsState!, e);
        _statsState = step.State;
        var sheet = _stats.Snapshot(_statsState);
        _latestSheet = sheet;
        var (next, found) = _detector.Apply(_detectorState!, e, sheet, step.Tags);
        _detectorState = next;
        _moments.AddRange(found);
        _lastSeq = e.Seq;
        _period = e.Period;
        _clockMs = e.ClockMs;

        var status = e.Type == EventType.FullTime
            ? MatchStatusTransition.Resolve(_status, MatchStatus.Completed)
            : _status is MatchStatus.Scheduled ? MatchStatus.Live : _status;
        var statusChanged = status != _status;
        _status = status;
        if (!live)
        {
            return [.. found.Select(moment => (moment, sheet))];
        }

        _outbox.AppendEvent(e);
        Push(ct => _broadcaster.EventAsync(e, ct));

        _eventsSinceSheet++;
        if (found.Count > 0 || _eventsSinceSheet >= FactSheetEveryEvents)
        {
            _eventsSinceSheet = 0;
            Push(ct => _broadcaster.FactSheetAsync(sheet, ct));
        }

        var minute = (e.Period * 1000) + (e.ClockMs / 60_000);
        if (found.Count > 0 || minute != _lastSavedMinute || statusChanged)
        {
            _lastSavedMinute = minute;
            Persist("fact sheet", ct => _store.SaveFactSheetAsync(sheet, ct));
            var lastSeq = _lastSeq;
            Persist("match status", ct => _store.UpdateMatchStatusAsync(_matchId, status, lastSeq, ct));
        }

        foreach (var moment in found)
        {
            Persist("moment", ct => _store.SaveMomentAsync(moment, ct));
            Push(ct => _broadcaster.MomentAsync(moment, ct));
            // The crew gets the moment through the write chain, after the moment and its fact sheet are stored, so
            // everything downstream of a narrative (the overlay's chips) can read them.
            var item = new MomentWorkItem(moment, sheet, _match!.Document);
            Persist("hand to the crew", ct => _sink.EnqueueAsync(item, ct).AsTask());
        }

        if (statusChanged)
        {
            var view = View().ToStatus();
            Push(ct => _broadcaster.StatusAsync(view, ct));
        }

        return [];
    }

    private MatchStateView View() => new(_matchId, _status, _lastSeq, _period, _clockMs, _subscribers.Count, _dropped);

    /// <summary>Writes to the store off the actor thread, in order, retried until they land.</summary>
    private void Persist(string what, Func<CancellationToken, Task> write) => _outbox.Write(what, write);

    /// <summary>Pushes to watchers and the crew in the order the actor produced them, off the actor thread.</summary>
    private void Push(Func<CancellationToken, Task> send) => _outbox.Push(send);
}
