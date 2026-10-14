using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Broadcast;

/// <summary>Where the crew's narratives go in the API: the <c>narratives</c> container, then the match's watchers.</summary>
public sealed class StoreAndBroadcastNarrativePublisher(IMatchStore store, IMatchBroadcaster broadcaster) : INarrativePublisher
{
    public async Task PublishAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        await store.SaveNarrativeAsync(narrative, cancellationToken).ConfigureAwait(false);
        await broadcaster.NarrativeAsync(narrative, cancellationToken).ConfigureAwait(false);
    }
}
