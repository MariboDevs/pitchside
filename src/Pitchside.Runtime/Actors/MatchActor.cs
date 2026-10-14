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
/// On (re)start it rebuilds its state by replaying the stored events.
/// </summary>
public sealed class MatchActor : ReceiveActor, IWithUnboundedStash, IWithTimers
{
    public const int FactSheetEveryEvents = 5;
    public const int MaxLoadAttempts = 40;
    public static readonly TimeSpan LoadRetryDelay = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(10);

    private readonly string _matchId;
    private readonly IMatchStore _store;
    private readonly IMatchBroadcaster _broadcaster;
    private readonly IMomentSink _sink;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IMatchStatsEngine _stats = new MatchStatsEngine();
    private readonly IMomentDetector _detector = new MomentDetector();
    private readonly List<Moment> _moments = [];
    private readonly SortedDictionary<int, MatchEvent> _pending = [];
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
    private Task _outbox = Task.CompletedTask;

    public MatchActor(string matchId, IMatchStore store, IMatchBroadcaster broadcaster, IMomentSink sink)
    {
        _matchId = matchId;
        _store = store;
        _broadcaster = broadcaster;
        _sink = sink;
        Become(Loading);
    }

    public IStash Stash { get; set; } = null!;

    public ITimerScheduler Timers { get; set; } = null!;

    public static Props Props(string matchId, IMatchStore store, IMatchBroadcaster broadcaster, IMomentSink sink) =>
        Akka.Actor.Props.Create(() => new MatchActor(matchId, store, broadcaster, sink));

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
        Receive<GetState>(_ => Sender.Tell(View()));
        Receive<GetFactSheet>(_ => Sender.Tell(_latestSheet ?? _stats.Snapshot(_statsState!)));
        Receive<GetMoments>(_ => Sender.Tell(_moments.ToList()));
        Receive<Subscribe>(message => _subscribers.Add(message.ConnectionId));
        Receive<Unsubscribe>(message => _subscribers.Remove(message.ConnectionId));
        Receive<StatusTick>(_ => Push(ct => _broadcaster.StatusAsync(View().ToStatus(), ct)));
        Receive<PersistFailed>(failed => _log.Warning(failed.Cause, "Match {0}: {1} failed", _matchId, failed.What));
    }

    private void Load()
    {
        _loadAttempt++;
        LoadAsync().PipeTo(Self, success: loaded => loaded, failure: ex => new LoadFailed(ex));
    }

    private async Task<MatchLoaded> LoadAsync()
    {
        var match = await _store.GetMatchAsync(_matchId, CancellationToken.None).ConfigureAwait(false);
        var events = match is null ? [] : await _store.GetEventsAsync(_matchId, 1, int.MaxValue, CancellationToken.None).ConfigureAwait(false);
        return new MatchLoaded(match, events);
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
        foreach (var e in loaded.Events)
        {
            if (e.Seq != expected)
            {
                break;
            }

            Apply(e, live: false);
            expected++;
        }

        if (loaded.Events.Count > 0)
        {
            _log.Info("Match {0} rebuilt from {1} stored events, last seq {2}", _matchId, _lastSeq, _lastSeq);
        }

        Become(Live);
        Timers.StartPeriodicTimer("status", StatusTick.Instance, StatusInterval);
        Stash.UnstashAll();
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
                _gapRequested = true;
                _log.Warning("GapDetected: match {0} expected seq {1}, got {2}; requesting a resend from the store", _matchId, _lastSeq + 1, e.Seq);
                _store.GetEventsAsync(_matchId, _lastSeq + 1, e.Seq - 1, CancellationToken.None)
                    .PipeTo(Self, success: events => new ResendLoaded(events), failure: ex => new PersistFailed("resend", ex));
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

    private void OnResend(ResendLoaded resend)
    {
        _gapRequested = false;
        foreach (var e in resend.Events.OrderBy(e => e.Seq))
        {
            Ingest(e);
        }
    }

    private void Apply(MatchEvent e, bool live)
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

        var status = e.Type == EventType.FullTime ? MatchStatus.Completed : _status is MatchStatus.Scheduled ? MatchStatus.Live : _status;
        var statusChanged = status != _status;
        _status = status;
        if (!live)
        {
            return;
        }

        Persist("event", ct => _store.AppendEventAsync(e, ct));
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
            var item = new MomentWorkItem(moment, sheet, _match!.Document);
            Push(ct => _sink.EnqueueAsync(item, ct).AsTask());
        }

        if (statusChanged)
        {
            var view = View().ToStatus();
            Push(ct => _broadcaster.StatusAsync(view, ct));
        }
    }

    private MatchStateView View() => new(_matchId, _status, _lastSeq, _period, _clockMs, _subscribers.Count, _dropped);

    /// <summary>Writes to the store off the actor thread; a failure comes back as a message.</summary>
    private void Persist(string what, Func<CancellationToken, Task> write) =>
        write(CancellationToken.None).PipeTo(Self, success: null, failure: ex => new PersistFailed(what, ex));

    /// <summary>
    /// Pushes to watchers and the crew in the order the actor produced the messages, off the actor thread: each
    /// send waits for the previous one, so clients see Event, FactSheet and Moment in sequence.
    /// </summary>
    private void Push(Func<CancellationToken, Task> send)
    {
        var self = Self;
        _outbox = SendAfter(_outbox, send, self);
    }

    private static async Task SendAfter(Task previous, Func<CancellationToken, Task> send, IActorRef self)
    {
        await previous.ConfigureAwait(false);
        try
        {
            await send(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            self.Tell(new PersistFailed("push", ex));
        }
    }
}
