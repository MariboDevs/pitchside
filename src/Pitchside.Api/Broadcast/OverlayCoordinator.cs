using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Pitchside.Contracts;
using Pitchside.Overlay;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Replay;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Broadcast;

/// <summary>
/// Puts verified base narratives on the overlay timeline and carries out the producer's controls, storing and
/// pushing every entry it writes. The scheduler itself is pure code in <c>Pitchside.Overlay</c>.
/// <para>
/// Each match's changes run one at a time, from the book update through the store write and the push, so a slow write
/// can never land after, and undo, a later hold or air. A match's stored timeline is read back the first time this
/// process touches it, so a restart keeps every card. The approval mode is process state: the API runs one replica
/// (WP-09), and after a restart a match starts in auto-air.
/// </para>
/// </summary>
public sealed class OverlayCoordinator(OverlayBook book, IMatchStore store, IMatchBroadcaster broadcaster, IRequiredActor<MatchSupervisorActor> supervisor, IReplayService replays)
{
    /// <summary>How long to wait for a moment and its fact sheet to be readable before giving up on a card.</summary>
    public static readonly TimeSpan InputWait = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public async Task ScheduleAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        if (narrative.Persona != Personas.Base || narrative.Language != Languages.English
            || narrative.Status is not (NarrativeStatus.Verified or NarrativeStatus.Fallback))
        {
            return;
        }

        var inputs = await InputsAsync(narrative, cancellationToken).ConfigureAwait(false);
        if (inputs is not var (match, moment, sheet))
        {
            return;
        }

        await SerialAsync(narrative.MatchId, async () =>
        {
            var state = await StateAsync(narrative.MatchId, cancellationToken).ConfigureAwait(false);
            await WriteAsync(book.Schedule(moment, narrative, sheet, match.Document, ReadyClock(state, moment)), cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The match clock the narrative was ready at, when that is a real-time clock: the match is still in play, in the
    /// moment's period, and not replaying as fast as possible. A finished match or an instant replay has a clock far
    /// past its moments, so its cards are laid out from their moments instead, as a record of the match.
    /// </summary>
    private int? ReadyClock(MatchStateView state, Moment moment) =>
        state.Status is MatchStatus.Live or MatchStatus.Paused && state.Period == moment.Period && !replays.IsInstant(state.MatchId)
            ? state.ClockMs
            : null;

    public async Task<OverlayEntry?> HoldAsync(string matchId, string entryId, CancellationToken cancellationToken)
    {
        if (await store.GetMatchAsync(matchId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        return await SerialAsync(matchId, async () =>
        {
            var (period, clockMs) = Clock(await StateAsync(matchId, cancellationToken).ConfigureAwait(false));
            var entry = book.Hold(matchId, entryId, period, clockMs);
            await WriteAsync(entry is null ? [] : [entry], cancellationToken).ConfigureAwait(false);
            return entry;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OverlayEntry?> AirAsync(string matchId, string entryId, CancellationToken cancellationToken)
    {
        if (await store.GetMatchAsync(matchId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        return await SerialAsync(matchId, async () =>
        {
            var state = await StateAsync(matchId, cancellationToken).ConfigureAwait(false);
            var written = book.Air(matchId, entryId, Math.Max(1, state.Period), state.ClockMs);
            await WriteAsync(written, cancellationToken).ConfigureAwait(false);
            return written.LastOrDefault(e => e.Id == entryId);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetModeAsync(string matchId, ApprovalMode mode, CancellationToken cancellationToken) =>
        await SerialAsync(matchId, async () =>
        {
            book.SetMode(matchId, mode);
            await broadcaster.ApprovalModeAsync(new ApprovalModeUpdate(matchId, mode), cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken).ConfigureAwait(false);

    public ApprovalMode Mode(string matchId) => book.Mode(matchId);

    /// <summary>Every entry, with scheduled ones whose out time has passed reading as aired.</summary>
    public async Task<IReadOnlyList<OverlayEntry>> EntriesAsync(string matchId, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(matchId, cancellationToken).ConfigureAwait(false);
        var (period, clockMs) = Clock(await StateAsync(matchId, cancellationToken).ConfigureAwait(false));
        return [.. book.Entries(matchId).Select(e => OverlayScheduler.Resolve(e, period, clockMs))];
    }

    /// <summary>The clock entries resolve against; a finished match has played its whole timeline.</summary>
    private static (int Period, int ClockMs) Clock(MatchStateView state) =>
        state.Status is MatchStatus.Completed or MatchStatus.Stopped ? (state.Period + 1, 0) : (state.Period, state.ClockMs);

    /// <summary>
    /// The match, the moment and the fact sheet at the narrative's sequence. The crew only receives a moment once
    /// those are written, but a store can lag its own writes briefly, so a missing one is read again before giving up.
    /// </summary>
    private async Task<(StoredMatch Match, Moment Moment, FactSheet Sheet)?> InputsAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + InputWait;
        while (true)
        {
            var match = await store.GetMatchAsync(narrative.MatchId, cancellationToken).ConfigureAwait(false);
            var moment = (await store.GetMomentsAsync(narrative.MatchId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(m => m.Id == narrative.MomentId);
            var sheet = await store.GetFactSheetAsync(narrative.MatchId, narrative.FactSheetSeq, cancellationToken).ConfigureAwait(false);
            if (match is not null && moment is not null && sheet?.AsOfSeq == narrative.FactSheetSeq)
            {
                return (match, moment, sheet);
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return null;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<T> SerialAsync<T>(string matchId, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(matchId, cancellationToken).ConfigureAwait(false);
        var gate = _gates.GetOrAdd(matchId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(string matchId, CancellationToken cancellationToken)
    {
        if (!book.IsLoaded(matchId))
        {
            var stored = await store.GetOverlayEntriesAsync(matchId, cancellationToken).ConfigureAwait(false);
            var moments = await store.GetMomentsAsync(matchId, cancellationToken).ConfigureAwait(false);
            book.Load(matchId, stored, moments);
        }
    }

    /// <summary>
    /// The match's status and clock from its actor. An actor deep in an instant replay's queue may not answer in time;
    /// the stored status and the latest stored fact sheet's period and clock, a little behind, stand in rather than
    /// failing the card, the hold, the air or the timeline read.
    /// </summary>
    private async Task<MatchStateView> StateAsync(string matchId, CancellationToken cancellationToken)
    {
        try
        {
            var matches = await supervisor.GetAsync(cancellationToken).ConfigureAwait(false);
            return await matches.Ask<MatchStateView>(new GetState(matchId), Hubs.MatchSnapshots.AskTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (AskTimeoutException)
        {
            var match = await store.GetMatchAsync(matchId, cancellationToken).ConfigureAwait(false);
            var sheet = await store.GetFactSheetAsync(matchId, int.MaxValue, cancellationToken).ConfigureAwait(false);
            return new MatchStateView(matchId, match?.Status ?? MatchStatus.Scheduled, match?.LastSeq ?? 0, sheet?.Period ?? 0, sheet?.ClockMs ?? 0, 0, 0);
        }
    }

    /// <summary>
    /// Stores and pushes each entry in order, still under the match's gate. The book already holds the change, so a
    /// write or push that fails briefly is retried rather than left behind the book.
    /// </summary>
    private async Task WriteAsync(IReadOnlyList<OverlayEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            await WriteRetry.RunAsync(token => store.SaveOverlayEntryAsync(entry, token), WriteRetry.Delays, cancellationToken).ConfigureAwait(false);
            await WriteRetry.RunAsync(token => broadcaster.OverlayEntryAsync(entry, token), WriteRetry.Delays, cancellationToken).ConfigureAwait(false);
        }
    }
}
