using System.Collections.Concurrent;
using Pitchside.Contracts;

namespace Pitchside.Overlay;

/// <summary>
/// Each match's overlay timeline and approval mode, safe for concurrent narratives. Producer controls: hold a card
/// so it never airs, air a held card from the current clock, switch between auto-air and approve-before-air.
/// </summary>
public sealed class OverlayBook
{
    private readonly ConcurrentDictionary<string, MatchOverlay> _matches = new(StringComparer.Ordinal);

    public ApprovalMode Mode(string matchId) => Match(matchId).Mode;

    public void SetMode(string matchId, ApprovalMode mode)
    {
        var match = Match(matchId);
        lock (match)
        {
            match.Mode = mode;
        }
    }

    public IReadOnlyList<OverlayEntry> Entries(string matchId)
    {
        var match = Match(matchId);
        lock (match)
        {
            return [.. match.Entries.Values.OrderBy(e => e.Period).ThenBy(e => e.InAtMs)];
        }
    }

    /// <summary>Whether this process has the match's stored timeline; see <see cref="Load"/>.</summary>
    public bool IsLoaded(string matchId) => Match(matchId).Loaded;

    /// <summary>
    /// Restores a match's timeline from storage after a restart. Entries already in the book win, since they are newer.
    /// A restored card's deadline comes from its moment, never from its stored start, which may have moved; a card
    /// stored as scheduled past that deadline can only have been aired by the producer, so it keeps that override.
    /// </summary>
    public void Load(string matchId, IReadOnlyList<OverlayEntry> stored, IReadOnlyList<Moment> moments)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(moments);
        var byId = moments.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var match = Match(matchId);
        lock (match)
        {
            foreach (var entry in stored)
            {
                if (!match.Entries.TryAdd(entry.Id, entry))
                {
                    continue;
                }

                var deadline = byId.TryGetValue(entry.MomentId, out var moment)
                    ? OverlayScheduler.DeadlineFor(moment)
                    : entry.InAtMs - OverlayScheduler.LeadMs + OverlayScheduler.MaxDelayMs;
                match.Deadlines.TryAdd(entry.Id, entry.State == OverlayState.Scheduled && entry.InAtMs > deadline ? int.MaxValue : deadline);
            }

            match.Loaded = true;
        }
    }

    /// <summary>
    /// Schedules a verified narrative and returns every entry that was written. <paramref name="scale"/> is the match's
    /// time scale (see <see cref="OverlayScheduler"/>).
    /// </summary>
    public IReadOnlyList<OverlayEntry> Schedule(Moment moment, Narrative narrative, FactSheet sheet, MatchDocument document, int? readyClockMs = null, double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(moment);
        var match = Match(moment.MatchId);
        lock (match)
        {
            if (match.Entries.ContainsKey(OverlayScheduler.IdFor(moment.Id, OverlayLayer.LowerThird)))
            {
                return [];
            }

            var changed = OverlayScheduler.Schedule([.. match.Entries.Values], moment, narrative, sheet, document, match.Mode, readyClockMs, match.Deadlines, scale);
            match.Deadlines[OverlayScheduler.IdFor(moment.Id, OverlayLayer.LowerThird)] = OverlayScheduler.DeadlineFor(moment, scale);
            foreach (var entry in changed)
            {
                match.Entries[entry.Id] = entry;
            }

            return changed;
        }
    }

    /// <summary>Holds a card that has not aired yet at the given clock.</summary>
    public OverlayEntry? Hold(string matchId, string entryId, int period, int clockMs)
    {
        var match = Match(matchId);
        lock (match)
        {
            if (!match.Entries.TryGetValue(entryId, out var entry)
                || OverlayScheduler.Resolve(entry, period, clockMs).State is OverlayState.Aired or OverlayState.Expired)
            {
                return null;
            }

            return match.Entries[entryId] = entry with { State = OverlayState.Held };
        }
    }

    /// <summary>
    /// Releases a held card through the same placement as a new one: from its own slot if that is still ahead,
    /// otherwise from the given clock, queuing behind or pre-empting what is on the line. The producer chose to air
    /// it, so it never expires, however often a later card moves it. Returns every entry written, the released card last, or nothing if it was not held.
    /// </summary>
    public IReadOnlyList<OverlayEntry> Air(string matchId, string entryId, int period, int clockMs, double scale = 1)
    {
        var match = Match(matchId);
        lock (match)
        {
            if (!match.Entries.TryGetValue(entryId, out var entry) || entry.State != OverlayState.Held)
            {
                return [];
            }

            var length = entry.OutAtMs - entry.InAtMs;
            var start = entry.Period == period && entry.InAtMs > clockMs ? entry.InAtMs : clockMs + OverlayScheduler.Scaled(OverlayScheduler.LeadMs, scale);
            var candidate = entry with { State = OverlayState.Scheduled, Period = period, InAtMs = start, OutAtMs = start + length };
            match.Deadlines[entryId] = int.MaxValue;
            var changed = OverlayScheduler.Place([.. match.Entries.Values], candidate, int.MaxValue, match.Deadlines, scale);
            foreach (var written in changed)
            {
                match.Entries[written.Id] = written;
            }

            return changed;
        }
    }

    private MatchOverlay Match(string matchId) => _matches.GetOrAdd(matchId, _ => new MatchOverlay());
}
