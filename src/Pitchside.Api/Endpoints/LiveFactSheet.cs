using Akka.Actor;
using Akka.Hosting;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Endpoints;

/// <summary>
/// A match's live fact sheet, from its actor. An actor deep in an instant replay's queue may not answer in time; the
/// latest stored sheet, a few events behind, stands in rather than failing the read.
/// </summary>
public static class LiveFactSheet
{
    public static async Task<FactSheet> GetAsync(IRequiredActor<MatchSupervisorActor> supervisor, IMatchStore store, StoredMatch match, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(supervisor);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(match);
        try
        {
            var matches = await supervisor.GetAsync(cancellationToken).ConfigureAwait(false);
            return await matches.Ask<FactSheet>(new GetFactSheet(match.MatchId), Hubs.MatchSnapshots.AskTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (AskTimeoutException)
        {
            return await store.GetFactSheetAsync(match.MatchId, int.MaxValue, cancellationToken).ConfigureAwait(false)
                ?? await RecomputedFacts.AtAsync(store, match, match.LastSeq, cancellationToken).ConfigureAwait(false);
        }
    }
}
