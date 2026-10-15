using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Broadcast;

/// <summary>
/// Where the crew's narratives go in the API: the <c>narratives</c> container, the match's watchers, and, for the
/// verified base narrative, the overlay timeline. Verified narratives are then queued for groundedness scoring.
/// </summary>
public sealed class StoreAndBroadcastNarrativePublisher(IMatchStore store, IMatchBroadcaster broadcaster, OverlayCoordinator overlay, GroundednessBatcher groundedness) : INarrativePublisher
{
    public async Task PublishAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        await store.SaveNarrativeAsync(narrative, cancellationToken).ConfigureAwait(false);
        await broadcaster.NarrativeAsync(narrative, cancellationToken).ConfigureAwait(false);
        await overlay.ScheduleAsync(narrative, cancellationToken).ConfigureAwait(false);
        groundedness.Enqueue(narrative);
    }
}
