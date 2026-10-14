using Akka.Actor;
using Akka.Hosting;
using Microsoft.AspNetCore.SignalR;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;

namespace Pitchside.Api.Hubs;

/// <summary>
/// The live match hub at <c>/hubs/match</c>. A client joins a match's group and is complete after the snapshot it
/// gets back; from then on it receives every event, fact sheet, moment, narrative and status as they happen.
/// </summary>
public sealed class MatchHub(MatchSnapshots snapshots, ViewerRegistry viewers, IRequiredActor<MatchSupervisorActor> supervisor) : Hub<IMatchClient>
{
    public const string Path = "/hubs/match";

    public static string Group(string matchId) => $"match:{matchId}";

    public async Task<MatchSnapshot> JoinMatch(string matchId)
    {
        var snapshot = await snapshots.BuildAsync(matchId, viewers.Get(Context.ConnectionId, matchId), Context.ConnectionAborted).ConfigureAwait(false)
            ?? throw new HubException($"No match '{matchId}'.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(matchId), Context.ConnectionAborted).ConfigureAwait(false);
        (await supervisor.GetAsync(Context.ConnectionAborted).ConfigureAwait(false)).Tell(new Subscribe(matchId, Context.ConnectionId), ActorRefs.NoSender);
        return snapshot;
    }

    public async Task LeaveMatch(string matchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(matchId), Context.ConnectionAborted).ConfigureAwait(false);
        viewers.Remove(Context.ConnectionId, matchId);
        (await supervisor.GetAsync(Context.ConnectionAborted).ConfigureAwait(false)).Tell(new Unsubscribe(matchId, Context.ConnectionId), ActorRefs.NoSender);
    }

    public Task SetViewer(string matchId, ViewerPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        viewers.Set(Context.ConnectionId, matchId, preferences);
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        viewers.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
