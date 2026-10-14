using System.Text.RegularExpressions;
using Akka.Actor;
using Akka.Event;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.MomentSink;
using Pitchside.Runtime.Replay;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Actors;

/// <summary>
/// Creates and supervises one <see cref="MatchActor"/> per match id, named <c>match-{matchId}</c>, and routes every
/// <see cref="IMatchMessage"/> to it. A failing match actor restarts up to five times in a minute; after that it
/// stops and the failure is logged at Error. Routing by match id is the seam for cluster sharding later.
/// Each match's <see cref="MatchOutbox"/> lives here, so ordered writes and pushes carry across restarts. A match id
/// must be a plain token; anything else is refused before it can name an actor. Unsubscribing and lifecycle changes
/// never create a match actor.
/// </summary>
public sealed partial class MatchSupervisorActor : ReceiveActor
{
    public const int MaxRestarts = 5;
    public static readonly TimeSpan RestartWindow = TimeSpan.FromMinutes(1);

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IMatchStore _store;
    private readonly IMatchBroadcaster _broadcaster;
    private readonly IMomentSink _sink;
    private readonly DurableProgress _progress;
    private readonly Dictionary<string, MatchOutbox> _outboxes = new(StringComparer.Ordinal);

    public MatchSupervisorActor(IMatchStore store, IMatchBroadcaster broadcaster, IMomentSink sink, IReplayService replay, DurableProgress progress)
    {
        _store = store;
        _broadcaster = broadcaster;
        _sink = sink;
        _progress = progress;

        Receive<StartReplay>(start => replay.StartAsync(start.Seed, start.Speed, CancellationToken.None)
            .PipeTo(Sender, success: started => started, failure: ex => new Status.Failure(ex)));
        Receive<StopReplay>(stop => Sender.Tell(replay.Stop(stop.MatchId)));
        Receive<IMatchMessage>(message => !IsValidMatchId(message.MatchId), message =>
        {
            _log.Warning("Refused a {0} for an invalid match id", message.GetType().Name);
            Sender.Tell(new Status.Failure(new ArgumentException("Not a valid match id.", nameof(message))));
        });
        Receive<Unsubscribe>(message => Existing(message.MatchId)?.Forward(message));
        Receive<SetLifecycle>(message => Existing(message.MatchId)?.Forward(message));
        Receive<IMatchMessage>(message => Child(message.MatchId).Forward(message));
        Receive<Terminated>(terminated =>
        {
            _log.Error("Match actor {0} stopped after repeated failures", terminated.ActorRef.Path.Name);
            var matchId = terminated.ActorRef.Path.Name["match-".Length..];
            if (_outboxes.Remove(matchId, out var outbox))
            {
                outbox.Dispose();
            }
        });
    }

    public static string ChildName(string matchId) => $"match-{matchId}";

    /// <summary>Letters, digits, hyphen and underscore, up to 64 characters: always a valid, unambiguous actor name.</summary>
    public static bool IsValidMatchId(string? matchId) => matchId is not null && MatchIdPattern().IsMatch(matchId);

    protected override void PostStop()
    {
        foreach (var outbox in _outboxes.Values)
        {
            outbox.Dispose();
        }

        _outboxes.Clear();
        base.PostStop();
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(MaxRestarts, RestartWindow, _ => Directive.Restart);

    private IActorRef? Existing(string matchId)
    {
        var child = Context.Child(ChildName(matchId));
        return child.IsNobody() ? null : child;
    }

    private IActorRef Child(string matchId)
    {
        var name = ChildName(matchId);
        var child = Context.Child(name);
        if (!child.IsNobody())
        {
            return child;
        }

        if (!_outboxes.TryGetValue(matchId, out var outbox))
        {
            outbox = new MatchOutbox(matchId, _store, _progress, _log);
            _outboxes[matchId] = outbox;
        }

        child = Context.ActorOf(MatchActor.Props(matchId, _store, _broadcaster, _sink, outbox), name);
        Context.Watch(child);
        return child;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex MatchIdPattern();
}
