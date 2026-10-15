using Pitchside.Contracts;
using Pitchside.Runtime.Storage;
using Pitchside.Stats;

namespace Pitchside.Api.Endpoints;

/// <summary>A match's fact sheet as of a sequence, recomputed exactly from the stored events by the stats engine.</summary>
public static class RecomputedFacts
{
    public static async Task<FactSheet> AtAsync(IMatchStore store, StoredMatch match, int atSeq, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(match);
        var engine = new MatchStatsEngine();
        var state = engine.Start(match.Document);
        foreach (var e in await store.GetEventsAsync(match.MatchId, 1, atSeq, cancellationToken).ConfigureAwait(false))
        {
            state = engine.Apply(state, e).State;
        }

        return engine.Snapshot(state);
    }
}
