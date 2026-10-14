using Akka.Actor;
using Akka.Hosting;
using Pitchside.Contracts;
using Pitchside.Overlay;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Broadcast;

/// <summary>
/// Puts verified base narratives on the overlay timeline and carries out the producer's controls, storing and
/// pushing every entry it writes. The scheduler itself is pure code in <c>Pitchside.Overlay</c>.
/// </summary>
public sealed class OverlayCoordinator(OverlayBook book, IMatchStore store, IMatchBroadcaster broadcaster, IRequiredActor<MatchSupervisorActor> supervisor)
{
    public async Task ScheduleAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        if (narrative.Persona != Personas.Base || narrative.Language != Languages.English
            || narrative.Status is not (NarrativeStatus.Verified or NarrativeStatus.Fallback))
        {
            return;
        }

        var match = await store.GetMatchAsync(narrative.MatchId, cancellationToken).ConfigureAwait(false);
        var moment = (await store.GetMomentsAsync(narrative.MatchId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(m => m.Id == narrative.MomentId);
        var sheet = await store.GetFactSheetAsync(narrative.MatchId, narrative.FactSheetSeq, cancellationToken).ConfigureAwait(false);
        if (match is null || moment is null || sheet is null)
        {
            return;
        }

        await WriteAsync(book.Schedule(moment, narrative, sheet, match.Document), cancellationToken).ConfigureAwait(false);
    }

    public async Task<OverlayEntry?> HoldAsync(string matchId, string entryId, CancellationToken cancellationToken)
    {
        var (period, clockMs) = Clock(await StateAsync(matchId, cancellationToken).ConfigureAwait(false));
        var entry = book.Hold(matchId, entryId, period, clockMs);
        await WriteAsync(entry is null ? [] : [entry], cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<OverlayEntry?> AirAsync(string matchId, string entryId, CancellationToken cancellationToken)
    {
        var state = await StateAsync(matchId, cancellationToken).ConfigureAwait(false);
        var entry = book.Air(matchId, entryId, Math.Max(1, state.Period), state.ClockMs);
        await WriteAsync(entry is null ? [] : [entry], cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task SetModeAsync(string matchId, ApprovalMode mode, CancellationToken cancellationToken)
    {
        book.SetMode(matchId, mode);
        await broadcaster.ApprovalModeAsync(new ApprovalModeUpdate(matchId, mode), cancellationToken).ConfigureAwait(false);
    }

    public ApprovalMode Mode(string matchId) => book.Mode(matchId);

    /// <summary>Every entry, with scheduled ones whose out time has passed reading as aired.</summary>
    public async Task<IReadOnlyList<OverlayEntry>> EntriesAsync(string matchId, CancellationToken cancellationToken)
    {
        var (period, clockMs) = Clock(await StateAsync(matchId, cancellationToken).ConfigureAwait(false));
        return [.. book.Entries(matchId).Select(e => OverlayScheduler.Resolve(e, period, clockMs))];
    }

    /// <summary>The clock entries resolve against; a finished match has played its whole timeline.</summary>
    private static (int Period, int ClockMs) Clock(MatchStateView state) =>
        state.Status is MatchStatus.Completed or MatchStatus.Stopped ? (state.Period + 1, 0) : (state.Period, state.ClockMs);

    private async Task<MatchStateView> StateAsync(string matchId, CancellationToken cancellationToken)
    {
        var matches = await supervisor.GetAsync(cancellationToken).ConfigureAwait(false);
        return await matches.Ask<MatchStateView>(new GetState(matchId), Hubs.MatchSnapshots.AskTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteAsync(IReadOnlyList<OverlayEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            await store.SaveOverlayEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            await broadcaster.OverlayEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        }
    }
}
