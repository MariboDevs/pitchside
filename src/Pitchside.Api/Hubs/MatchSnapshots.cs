using Akka.Actor;
using Akka.Hosting;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Hubs;

/// <summary>
/// Builds the one message a late joiner needs: the match, the live fact sheet, every moment, the narratives for its
/// viewer preferences (and the base English ones every view falls back to), the overlay timeline and the approval mode.
/// The fact sheet, the moments, the status and the last seq come from the match actor in one reply, so they agree with
/// each other even while their store writes are still landing.
/// </summary>
public sealed class MatchSnapshots(IMatchStore store, IRequiredActor<MatchSupervisorActor> supervisor, Broadcast.OverlayCoordinator overlay)
{
    public static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(3);

    public async Task<bool> ExistsAsync(string matchId, CancellationToken cancellationToken) =>
        await store.GetMatchAsync(matchId, cancellationToken).ConfigureAwait(false) is not null;

    public async Task<MatchSnapshot?> BuildAsync(string matchId, ViewerPreferences viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var match = await store.GetMatchAsync(matchId, cancellationToken).ConfigureAwait(false);
        if (match is null)
        {
            return null;
        }

        var matches = await supervisor.GetAsync(cancellationToken).ConfigureAwait(false);
        var live = await matches.Ask<LiveState>(new GetLiveState(matchId), AskTimeout, cancellationToken).ConfigureAwait(false);
        var narratives = await store.GetNarrativesAsync(matchId, cancellationToken).ConfigureAwait(false);
        var entries = await overlay.EntriesAsync(matchId, cancellationToken).ConfigureAwait(false);
        return new MatchSnapshot(
            match with { Status = live.View.Status, LastSeq = live.View.LastSeq },
            live.FactSheet,
            [.. live.Moments],
            [.. narratives.Where(n => (n.Persona == viewer.Persona && n.Language == viewer.Language)
                || (n.Persona == Personas.Base && n.Language == Languages.English))],
            entries,
            overlay.Mode(matchId));
    }
}
