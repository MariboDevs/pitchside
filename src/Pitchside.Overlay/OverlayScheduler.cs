using Pitchside.Contracts;
using Pitchside.Moments;

namespace Pitchside.Overlay;

/// <summary>
/// Places a lower third on the timeline. It shows for 6 s plus 0.25 s per word, capped at 12 s, from 2 s after its
/// narrative is ready. One lower third at a time: a higher priority pre-empts the card on screen (its out time is cut
/// and the new card starts after the exit animation, pushing queued cards back), a lower or equal priority takes the
/// first gap at or after its start that fits it with the exit gap, and a card that would start more than 45 s after
/// its moment expires, however often it is moved. Those times are seen on screen, so on a replay faster than real
/// time each is multiplied by the time scale (match milliseconds per real millisecond) to give the match-clock times
/// the timeline is kept in. Pure and deterministic.
/// </summary>
public static class OverlayScheduler
{
    public const int LeadMs = 2_000;
    public const int ExitMs = 200;
    public const int BaseMs = 6_000;
    public const int PerWordMs = 250;
    public const int CapMs = 12_000;
    public const int MaxDelayMs = 45_000;

    /// <summary>A real-time span as match-clock milliseconds at <paramref name="scale"/>.</summary>
    public static int Scaled(int realMs, double scale) => (int)Math.Round(realMs * scale);

    /// <summary>
    /// The entries to write: the new one, and any existing ones it moved. The card starts <see cref="LeadMs"/> after
    /// the narrative is ready (<paramref name="readyClockMs"/>, the match clock when it arrived, never earlier than
    /// the moment) and expires if it would start more than <see cref="MaxDelayMs"/> after its moment. In approve mode
    /// the card is held and touches nothing else until the producer airs it.
    /// </summary>
    public static IReadOnlyList<OverlayEntry> Schedule(
        IReadOnlyList<OverlayEntry> existing,
        Moment moment,
        Narrative narrative,
        FactSheet sheet,
        MatchDocument document,
        ApprovalMode mode,
        int? readyClockMs = null,
        IReadOnlyDictionary<string, int>? deadlines = null,
        double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(narrative);

        var duration = Scaled(DurationFor(narrative.RenderedText), scale);
        var inAt = Math.Max(moment.ClockMs, readyClockMs ?? moment.ClockMs) + Scaled(LeadMs, scale);
        var candidate = new OverlayEntry
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
            State = OverlayState.Scheduled,
            FullWidth = moment.Type == MomentTypes.FullTime,
        };

        if (mode == ApprovalMode.Approve)
        {
            return [candidate with { State = OverlayState.Held }];
        }

        return Place(existing, candidate, DeadlineFor(moment, scale), deadlines ?? new Dictionary<string, int>(), scale);
    }

    /// <summary>The latest start a moment's card may have: <see cref="MaxDelayMs"/> of real time after the moment.</summary>
    public static int DeadlineFor(Moment moment, double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(moment);
        return moment.ClockMs + Scaled(MaxDelayMs, scale);
    }

    /// <summary>
    /// Puts a card on the line from its <c>InAtMs</c>. A higher priority than the card on screen pre-empts it: that
    /// card's out time is cut, this one starts after the exit, and queued cards move back, expiring past their own
    /// deadline. Otherwise the card takes the first gap from its start that fits it, always leaving the exit
    /// animation's gap. A card that would start after <paramref name="deadline"/> expires, and an overdue card never
    /// pre-empts: it moves nothing.
    /// </summary>
    public static IReadOnlyList<OverlayEntry> Place(
        IReadOnlyList<OverlayEntry> existing, OverlayEntry candidate, int deadline, IReadOnlyDictionary<string, int> deadlines, double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(deadlines);

        var exit = Scaled(ExitMs, scale);
        var desired = candidate.InAtMs;
        var duration = candidate.OutAtMs - candidate.InAtMs;
        var line = existing
            .Where(e => e.Id != candidate.Id && e.Layer == OverlayLayer.LowerThird && e.Period == candidate.Period && e.State is OverlayState.Scheduled or OverlayState.Aired)
            .OrderBy(e => e.InAtMs)
            .ToList();
        var changed = new List<OverlayEntry>();
        var onScreen = line.FirstOrDefault(e => e.InAtMs <= desired && desired < e.OutAtMs);

        int inAt;
        if (onScreen is not null && candidate.Priority > onScreen.Priority && desired + exit <= deadline)
        {
            changed.Add(onScreen with { OutAtMs = desired });
            inAt = desired + exit;
            var cursor = inAt + duration;
            foreach (var queued in line.Where(e => e.InAtMs > desired))
            {
                var length = queued.OutAtMs - queued.InAtMs;
                var start = Math.Max(queued.InAtMs, cursor + exit);
                var limit = deadlines.TryGetValue(queued.Id, out var known) ? known : queued.InAtMs - Scaled(LeadMs, scale) + Scaled(MaxDelayMs, scale);
                var moved = queued with
                {
                    InAtMs = start,
                    OutAtMs = start + length,
                    State = queued.State == OverlayState.Scheduled && start > limit ? OverlayState.Expired : queued.State,
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
            inAt = FirstFit(line, desired, duration, exit);
        }

        changed.Add(candidate with
        {
            InAtMs = inAt,
            OutAtMs = inAt + duration,
            State = inAt > deadline ? OverlayState.Expired : OverlayState.Scheduled,
        });
        return changed;
    }

    /// <summary>
    /// The earliest start at or after <paramref name="desired"/> where the card fits between the cards on the line,
    /// with the exit gap on both sides. Narratives finish out of order, so a card can arrive after a later moment's
    /// card is already placed; it takes the free gap at its own moment rather than queuing behind the whole period.
    /// </summary>
    private static int FirstFit(List<OverlayEntry> line, int desired, int duration, int exit)
    {
        var start = desired;
        foreach (var placed in line)
        {
            if (placed.OutAtMs + exit <= start)
            {
                continue;
            }

            if (start + duration + exit <= placed.InAtMs)
            {
                break;
            }

            start = Math.Max(start, placed.OutAtMs + exit);
        }

        return start;
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
