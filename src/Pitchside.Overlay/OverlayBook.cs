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

    /// <summary>Schedules a verified narrative and returns every entry that was written.</summary>
    public IReadOnlyList<OverlayEntry> Schedule(Moment moment, Narrative narrative, FactSheet sheet, MatchDocument document)
    {
        ArgumentNullException.ThrowIfNull(moment);
        var match = Match(moment.MatchId);
        lock (match)
        {
            if (match.Entries.ContainsKey(OverlayScheduler.IdFor(moment.Id, OverlayLayer.LowerThird)))
            {
                return [];
            }

            var changed = OverlayScheduler.Schedule([.. match.Entries.Values], moment, narrative, sheet, document, match.Mode);
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

    /// <summary>Releases a held card: it keeps its slot if that is still ahead, otherwise starts from the given clock.</summary>
    public OverlayEntry? Air(string matchId, string entryId, int period, int clockMs)
    {
        var match = Match(matchId);
        lock (match)
        {
            if (!match.Entries.TryGetValue(entryId, out var entry) || entry.State != OverlayState.Held)
            {
                return null;
            }

            var length = entry.OutAtMs - entry.InAtMs;
            var start = entry.Period == period && entry.InAtMs > clockMs ? entry.InAtMs : clockMs + OverlayScheduler.LeadMs;
            return match.Entries[entryId] = entry with { State = OverlayState.Scheduled, Period = period, InAtMs = start, OutAtMs = start + length };
        }
    }

    private MatchOverlay Match(string matchId) => _matches.GetOrAdd(matchId, _ => new MatchOverlay());
}
