using System.Collections.Immutable;
using Pitchside.Contracts;

namespace Pitchside.Moments;

/// <summary>
/// The WP-02 rate limits. A side produces at most one <c>momentum_swing</c> and one <c>sustained_pressure</c> per
/// five minutes of period clock. Every type except goal, red card, penalty, half time and full time shares a
/// 90-second gap per match: inside it, a moment is dropped unless it outranks the one that opened the gap.
/// </summary>
public sealed record MomentRateLimiter
{
    public const int MatchGapMs = 90_000;
    public const int SideGapMs = 5 * 60_000;

    public static MomentRateLimiter Empty { get; } = new();

    public LimiterMark? LastLimited { get; init; }

    public ImmutableDictionary<string, LimiterMark> LastPerSide { get; init; } = ImmutableDictionary<string, LimiterMark>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>True when the per-side five-minute rule blocks this moment.</summary>
    public bool BlockedForSide(Moment moment)
    {
        ArgumentNullException.ThrowIfNull(moment);
        return IsPerSide(moment.Type)
            && LastPerSide.TryGetValue(SideKey(moment), out var mark)
            && mark.Period == moment.Period
            && moment.ClockMs - mark.ClockMs < SideGapMs;
    }

    /// <summary>True when the match-wide 90-second gap blocks this moment.</summary>
    public bool BlockedForMatch(Moment moment)
    {
        ArgumentNullException.ThrowIfNull(moment);
        return !MomentTypes.IsExempt(moment.Type)
            && LastLimited is { } mark
            && mark.Period == moment.Period
            && moment.ClockMs - mark.ClockMs < MatchGapMs
            && moment.Priority <= mark.Priority;
    }

    /// <summary>Records an emitted moment.</summary>
    public MomentRateLimiter Record(Moment moment)
    {
        ArgumentNullException.ThrowIfNull(moment);
        var next = this;
        if (!MomentTypes.IsExempt(moment.Type))
        {
            next = next with { LastLimited = new LimiterMark(moment.Period, moment.ClockMs, moment.Priority) };
        }

        if (IsPerSide(moment.Type))
        {
            next = next with { LastPerSide = next.LastPerSide.SetItem(SideKey(moment), new LimiterMark(moment.Period, moment.ClockMs, moment.Priority)) };
        }

        return next;
    }

    private static bool IsPerSide(string type) => type is MomentTypes.MomentumSwing or MomentTypes.SustainedPressure;

    private static string SideKey(Moment moment) => $"{moment.Type}|{moment.SideId}";
}
