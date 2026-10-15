using Microsoft.AspNetCore.SignalR;
using Pitchside.Api.Hubs;
using Pitchside.Contracts;
using Pitchside.Runtime.Broadcast;

namespace Pitchside.Api.Broadcast;

/// <summary>
/// Pushes to the SignalR group of the match a message belongs to. Narratives are the exception: each goes only to the
/// connections whose viewer preferences it matches (<see cref="NarrativeAudience"/>), so a viewer receives its own
/// persona and language and the control room receives them all.
/// </summary>
public sealed class SignalRMatchBroadcaster(IHubContext<MatchHub, IMatchClient> hub, ViewerRegistry viewers) : IMatchBroadcaster
{
    public Task EventAsync(MatchEvent matchEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(matchEvent);
        return hub.Clients.Group(MatchHub.Group(matchEvent.MatchId)).Event(matchEvent);
    }

    public Task FactSheetAsync(FactSheet sheet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return hub.Clients.Group(MatchHub.Group(sheet.MatchId)).FactSheet(sheet);
    }

    public Task MomentAsync(Moment moment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moment);
        return hub.Clients.Group(MatchHub.Group(moment.MatchId)).Moment(moment);
    }

    public Task NarrativeAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        var audience = viewers.Watching(narrative.MatchId)
            .Where(w => NarrativeAudience.Receives(w.Viewer, narrative))
            .Select(w => w.ConnectionId)
            .ToList();
        return audience.Count == 0 ? Task.CompletedTask : hub.Clients.Clients(audience).Narrative(narrative);
    }

    public Task StatusAsync(MatchStatusUpdate status, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(status);
        return hub.Clients.Group(MatchHub.Group(status.MatchId)).MatchStatus(status);
    }

    public Task OverlayEntryAsync(OverlayEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return hub.Clients.Group(MatchHub.Group(entry.MatchId)).OverlayEntry(entry);
    }

    public Task ApprovalModeAsync(ApprovalModeUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        return hub.Clients.Group(MatchHub.Group(update.MatchId)).ApprovalMode(update);
    }
}
