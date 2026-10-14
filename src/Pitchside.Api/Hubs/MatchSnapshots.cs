using Akka.Actor;
using Akka.Hosting;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Hubs;

/// <summary>Builds the one message a late joiner needs: the match, the live fact sheet, every moment, its narratives.</summary>
public sealed class MatchSnapshots(IMatchStore store, IRequiredActor<MatchSupervisorActor> supervisor)
{
    public static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    public async Task<MatchSnapshot?> BuildAsync(string matchId, ViewerPreferences viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var match = await store.GetMatchAsync(matchId, cancellationToken).ConfigureAwait(false);
        if (match is null)
        {
            return null;
        }

        var matches = await supervisor.GetAsync(cancellationToken).ConfigureAwait(false);
        var sheet = await matches.Ask<FactSheet>(new GetFactSheet(matchId), AskTimeout, cancellationToken).ConfigureAwait(false);
        var moments = await store.GetMomentsAsync(matchId, cancellationToken).ConfigureAwait(false);
        var narratives = await store.GetNarrativesAsync(matchId, cancellationToken).ConfigureAwait(false);
        return new MatchSnapshot(match, sheet, moments, [.. narratives.Where(n => n.Persona == viewer.Persona && n.Language == viewer.Language)]);
    }
}
