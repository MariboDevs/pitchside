using Akka.Actor;
using Akka.Hosting;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Hubs;

/// <summary>
/// Builds the one message a late joiner needs: the match, the live fact sheet, every moment, the narratives its viewer
/// preferences receive (<see cref="NarrativeAudience"/>), the overlay timeline and the approval mode.
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

        var narratives = await store.GetNarrativesAsync(matchId, cancellationToken).ConfigureAwait(false);
        var entries = await overlay.EntriesAsync(matchId, cancellationToken).ConfigureAwait(false);
        var (state, sheet, moments) = await LiveAsync(match, cancellationToken).ConfigureAwait(false);
        return new MatchSnapshot(
            state,
            sheet,
            moments,
            [.. narratives.Where(n => NarrativeAudience.Receives(viewer, n))],
            entries,
            overlay.Mode(matchId));
    }

    /// <summary>
    /// The match's status, fact sheet and moments from the match actor, which has them in agreement. An actor busy
    /// with a long queue (an instant replay of a whole match) may not answer in time; the store's copy, a little
    /// behind, stands in, and the pushes the connection already receives bring it up to date.
    /// </summary>
    private async Task<(StoredMatch Match, FactSheet? Sheet, IReadOnlyList<Moment> Moments)> LiveAsync(StoredMatch match, CancellationToken cancellationToken)
    {
        try
        {
            var matches = await supervisor.GetAsync(cancellationToken).ConfigureAwait(false);
            var live = await matches.Ask<LiveState>(new GetLiveState(match.MatchId), AskTimeout, cancellationToken).ConfigureAwait(false);
            return (match with { Status = live.View.Status, LastSeq = live.View.LastSeq }, live.FactSheet, [.. live.Moments]);
        }
        catch (AskTimeoutException)
        {
            // Read again: the store moved on while the actor did not answer, and the client loads history up to LastSeq.
            var latest = await store.GetMatchAsync(match.MatchId, cancellationToken).ConfigureAwait(false) ?? match;
            var sheet = await store.GetFactSheetAsync(match.MatchId, int.MaxValue, cancellationToken).ConfigureAwait(false);
            var moments = await store.GetMomentsAsync(match.MatchId, cancellationToken).ConfigureAwait(false);
            return (latest, sheet, moments);
        }
    }
}
