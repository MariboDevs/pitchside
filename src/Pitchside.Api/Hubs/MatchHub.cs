using Akka.Actor;
using Akka.Hosting;
using Microsoft.AspNetCore.SignalR;
using Pitchside.Api.Middleware;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Usage;

namespace Pitchside.Api.Hubs;

/// <summary>
/// The live match hub at <c>/hubs/match</c>. A client joins a match's group and is complete after the snapshot it
/// gets back; from then on it receives every event, fact sheet, moment, narrative and status as they happen. The
/// connection joins the group before the snapshot is read, so nothing falls between the two: a push that arrives
/// while the snapshot is built may also be in it, and clients drop what they already hold by id and seq.
/// </summary>
public sealed class MatchHub(MatchSnapshots snapshots, ViewerRegistry viewers, IRequiredActor<MatchSupervisorActor> supervisor, UsageGuard guard) : Hub<IMatchClient>
{
    public const string Path = "/hubs/match";

    private const string SlotReserved = "usage-slot";

    public static string Group(string matchId) => $"match:{matchId}";

    public async Task<MatchSnapshot> JoinMatch(string matchId)
    {
        if (!MatchSupervisorActor.IsValidMatchId(matchId) || !await snapshots.ExistsAsync(matchId, Context.ConnectionAborted).ConfigureAwait(false))
        {
            throw new HubException($"No match '{matchId}'.");
        }

        // Join the group and the narrative audience before the snapshot is read, so a narrative stored after the
        // snapshot's read is still pushed to this connection; anything in both is dropped by id on the client.
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(matchId), Context.ConnectionAborted).ConfigureAwait(false);
        var joined = viewers.Join(Context.ConnectionId, matchId);
        try
        {
            var snapshot = await snapshots.BuildAsync(matchId, viewers.Get(Context.ConnectionId, matchId), Context.ConnectionAborted).ConfigureAwait(false)
                ?? throw new HubException($"No match '{matchId}'.");
            (await supervisor.GetAsync(Context.ConnectionAborted).ConfigureAwait(false)).Tell(new Subscribe(matchId, Context.ConnectionId), ActorRefs.NoSender);
            return snapshot;
        }
        catch
        {
            if (joined)
            {
                viewers.Unjoin(Context.ConnectionId, matchId);
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(matchId), CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task LeaveMatch(string matchId)
    {
        if (!viewers.Leave(Context.ConnectionId, matchId))
        {
            return;
        }

        // Unsubscribe first and regardless of the connection: the join is already forgotten, so a disconnect that
        // aborted the rest of this call would otherwise leave the actor holding a stale subscriber.
        (await supervisor.GetAsync(CancellationToken.None).ConfigureAwait(false)).Tell(new Unsubscribe(matchId, Context.ConnectionId), ActorRefs.NoSender);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(matchId), Context.ConnectionAborted).ConfigureAwait(false);
    }

    public Task SetViewer(string matchId, ViewerPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        viewers.Set(Context.ConnectionId, matchId, preferences);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reserves one of the caller's connection slots, for every transport, and closes the connection when none is free.
    /// <see cref="UsageGuardMiddleware"/> refuses at negotiation as well, so a browser sees a 429 rather than a drop.
    /// </summary>
    public override Task OnConnectedAsync()
    {
        if (!guard.TryConnect(ClientKey.Of(Context.GetHttpContext())))
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        Context.Items[SlotReserved] = true;
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.ContainsKey(SlotReserved))
        {
            guard.Disconnected(ClientKey.Of(Context.GetHttpContext()));
        }

        var joined = viewers.RemoveConnection(Context.ConnectionId);
        if (joined.Count > 0)
        {
            var matches = await supervisor.GetAsync(CancellationToken.None).ConfigureAwait(false);
            foreach (var matchId in joined)
            {
                matches.Tell(new Unsubscribe(matchId, Context.ConnectionId), ActorRefs.NoSender);
            }
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }
}
