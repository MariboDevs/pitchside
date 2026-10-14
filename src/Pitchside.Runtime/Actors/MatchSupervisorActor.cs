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
/// </summary>
public sealed class MatchSupervisorActor : ReceiveActor
{
    public const int MaxRestarts = 5;
    public static readonly TimeSpan RestartWindow = TimeSpan.FromMinutes(1);

    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IMatchStore _store;
    private readonly IMatchBroadcaster _broadcaster;
    private readonly IMomentSink _sink;

    public MatchSupervisorActor(IMatchStore store, IMatchBroadcaster broadcaster, IMomentSink sink, IReplayService replay)
    {
        _store = store;
        _broadcaster = broadcaster;
        _sink = sink;

        Receive<StartReplay>(start => replay.StartAsync(start.Seed, start.Speed, CancellationToken.None)
            .PipeTo(Sender, success: started => started, failure: ex => new Status.Failure(ex)));
        Receive<StopReplay>(stop => Sender.Tell(replay.Stop(stop.MatchId)));
        Receive<IMatchMessage>(message => Child(message.MatchId).Forward(message));
        Receive<Terminated>(terminated => _log.Error("Match actor {0} stopped after repeated failures", terminated.ActorRef.Path.Name));
    }

    public static string ChildName(string matchId) => $"match-{matchId}";

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(MaxRestarts, RestartWindow, _ => Directive.Restart);

    private IActorRef Child(string matchId)
    {
        var name = ChildName(matchId);
        var child = Context.Child(name);
        if (!child.IsNobody())
        {
            return child;
        }

        child = Context.ActorOf(MatchActor.Props(matchId, _store, _broadcaster, _sink), name);
        Context.Watch(child);
        return child;
    }
}
