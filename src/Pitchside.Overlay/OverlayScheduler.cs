using Pitchside.Contracts;
using Pitchside.Moments;

namespace Pitchside.Overlay;

/// <summary>
/// Places a lower third on the timeline. It shows for 6 s plus 0.25 s per word, capped at 12 s, from 2 s after its
/// moment. One lower third at a time: a higher priority pre-empts the card on screen (its out time is cut and the
/// new card starts after the exit animation, pushing queued cards back), a lower or equal priority queues, and a
/// card that would start more than 45 s after its moment expires. Pure and deterministic.
/// </summary>
public static class OverlayScheduler
{
    public const int LeadMs = 2_000;
    public const int ExitMs = 200;
    public const int BaseMs = 6_000;
    public const int PerWordMs = 250;
    public const int CapMs = 12_000;
    public const int MaxDelayMs = 45_000;

    /// <summary>The entries to write: the new one, and any existing ones it moved.</summary>
    public static IReadOnlyList<OverlayEntry> Schedule(
        IReadOnlyList<OverlayEntry> existing, Moment moment, Narrative narrative, FactSheet sheet, MatchDocument document, ApprovalMode mode)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(narrative);

        var duration = DurationFor(narrative.RenderedText);
        var desired = moment.ClockMs + LeadMs;
        var line = existing
            .Where(e => e.Layer == OverlayLayer.LowerThird && e.Period == moment.Period && e.State is OverlayState.Scheduled or OverlayState.Aired)
            .OrderBy(e => e.InAtMs)
            .ToList();
        var changed = new List<OverlayEntry>();
        var onScreen = line.FirstOrDefault(e => e.InAtMs <= desired && desired < e.OutAtMs);

        int inAt;
        if (onScreen is not null && moment.Priority > onScreen.Priority)
        {
            changed.Add(onScreen with { OutAtMs = desired });
            inAt = desired + ExitMs;
            var cursor = inAt + duration;
            foreach (var queued in line.Where(e => e.InAtMs > desired))
            {
                var length = queued.OutAtMs - queued.InAtMs;
                var start = Math.Max(queued.InAtMs, cursor + ExitMs);
                var moved = queued with
                {
                    InAtMs = start,
                    OutAtMs = start + length,
                    State = start - (queued.InAtMs - LeadMs) > MaxDelayMs + LeadMs ? OverlayState.Expired : queued.State,
                };
                if (moved != queued)
                {
                    changed.Add(moved);
                }

                cursor = moved.State == OverlayState.Expired ? cursor : moved.OutAtMs;
            }
        }
        else
        {
            var lastOut = line.Count == 0 ? 0 : line.Max(e => e.OutAtMs);
            inAt = lastOut > desired ? lastOut + ExitMs : desired;
        }

        var state = inAt > moment.ClockMs + MaxDelayMs
            ? OverlayState.Expired
            : mode == ApprovalMode.Approve ? OverlayState.Held : OverlayState.Scheduled;
        changed.Add(new OverlayEntry
        {
            Id = IdFor(moment.Id, OverlayLayer.LowerThird),
            MatchId = moment.MatchId,
            MomentId = moment.Id,
            Layer = OverlayLayer.LowerThird,
            Slot = OverlaySlot.Bottom,
            Priority = moment.Priority,
            InAtMs = inAt,
            OutAtMs = inAt + duration,
            Period = moment.Period,
            NarrativeId = narrative.Id,
            Chips = OverlayChips.For(moment, sheet, document),
            State = state,
            FullWidth = moment.Type == MomentTypes.FullTime,
        });
        return changed;
    }

    /// <summary>6 s plus 0.25 s per word of the rendered text, capped at 12 s.</summary>
    public static int DurationFor(string renderedText)
    {
        var words = (renderedText ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Min(CapMs, BaseMs + (PerWordMs * words));
    }

    public static string IdFor(string momentId, OverlayLayer layer) => $"{momentId}:{LayerName(layer)}";

    /// <summary>
    /// The state an entry reads as at a period clock: a scheduled card whose out time has passed has aired. Pass the
    /// period after the last one to read a finished match, whose whole timeline has played.
    /// </summary>
    public static OverlayEntry Resolve(OverlayEntry entry, int period, int clockMs)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var past = entry.Period < period || (entry.Period == period && entry.OutAtMs <= clockMs);
        return entry.State == OverlayState.Scheduled && past ? entry with { State = OverlayState.Aired } : entry;
    }

    private static string LayerName(OverlayLayer layer) => layer switch
    {
        OverlayLayer.ScoreBug => "score_bug",
        OverlayLayer.LowerThird => "lower_third",
        OverlayLayer.StatChip => "stat_chip",
        _ => "ribbon",
    };
}
