using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Broadcast;

/// <summary>
/// Where the crew's narratives go in the API: the <c>narratives</c> container, the match's watchers, and, for the
/// verified base narrative, the overlay timeline. Verified narratives are then queued for groundedness scoring, once
/// saved, whatever happens to the push.
/// </summary>
public sealed class StoreAndBroadcastNarrativePublisher(IMatchStore store, IMatchBroadcaster broadcaster, OverlayCoordinator overlay, GroundednessBatcher groundedness) : INarrativePublisher
{
    public async Task PublishAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        await store.SaveNarrativeAsync(narrative, cancellationToken).ConfigureAwait(false);
        try
        {
            await broadcaster.NarrativeAsync(narrative, cancellationToken).ConfigureAwait(false);
            await overlay.ScheduleAsync(narrative, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Once saved, it is scored even if the push or the overlay fails: queued after them, so a scored version
            // pushed by the batcher never arrives before this unscored one.
            groundedness.Enqueue(narrative);
        }
    }
}
