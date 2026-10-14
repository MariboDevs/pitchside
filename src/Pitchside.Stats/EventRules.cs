using Pitchside.Contracts;

namespace Pitchside.Stats;

/// <summary>The definitions the metric catalogue relies on, in one place so the incremental and batch paths agree.</summary>
public static class EventRules
{
    public const int TenMinutesMs = 10 * 60_000;
    public const int FiveMinutesMs = 5 * 60_000;
    public const double BigChanceXg = 0.30;
    public const double ProgressiveGainM = 10;
    public const double FinalThirdX = 70;

    /// <summary>
    /// The side in possession that an event reveals, or null when it reveals none. Defensive actions (pressure,
    /// foul, card) and a corner awarded by the ball going out reveal the other side.
    /// </summary>
    public static string? PossessionOwner(MatchEvent e, string homeId, string awayId)
    {
        ArgumentNullException.ThrowIfNull(e);
        var other = e.TeamId == homeId ? awayId : homeId;
        return e.Type switch
        {
            EventType.KickOff or EventType.Pass or EventType.Carry or EventType.Shot or EventType.Goal
                or EventType.Turnover or EventType.Corner => e.TeamId,
            EventType.Pressure or EventType.Foul or EventType.Card => other,
            EventType.OutOfPlay => e.Restart == Restart.Corner ? other : e.TeamId,
            _ => null,
        };
    }

    /// <summary>Open-play events: the denominator of <c>match.control</c>.</summary>
    public static bool IsOpenPlay(MatchEvent e) => e.Type is EventType.Pass or EventType.Carry or EventType.Shot
        or EventType.Turnover or EventType.Pressure or EventType.Foul or EventType.OutOfPlay;

    /// <summary>Controlled events: the numerator of <c>match.control</c>.</summary>
    public static bool IsControlled(MatchEvent e) =>
        (e.Type == EventType.Pass && e.Completed == true) || e.Type == EventType.Carry;

    /// <summary>Events that count towards <c>match.rhythm</c>'s distinct possessions.</summary>
    public static bool CountsForRhythm(MatchEvent e) => e.PossessionId > 0 && e.Type is not (EventType.KickOff
        or EventType.Tracking or EventType.HalfEnd or EventType.FullTime or EventType.Substitution);

    public static bool IsProgressive(MatchEvent e) =>
        e.Type == EventType.Pass && e.Completed == true && e.EndLocation is { } end && end.X - e.Location.X >= ProgressiveGainM;

    public static bool IsFinalThirdEntry(MatchEvent e) =>
        ((e.Type == EventType.Pass && e.Completed == true) || e.Type == EventType.Carry)
        && e.EndLocation is { } end && e.Location.X <= FinalThirdX && end.X > FinalThirdX;

    /// <summary>
    /// A side's momentum weight for one event: shots 3, shots on target 4 more, final-third entries 1, corners 2,
    /// and 10 per unit of xG, summed.
    /// </summary>
    public static double MomentumWeight(MatchEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var weight = 0.0;
        if (e.Type == EventType.Shot)
        {
            weight += 3 + (e.OnTarget == true ? 4 : 0) + (10 * (e.Xg ?? 0));
        }

        if (e.Type == EventType.Corner)
        {
            weight += 2;
        }

        if (IsFinalThirdEntry(e))
        {
            weight += 1;
        }

        return weight;
    }

    /// <summary>Match minute: 1 to 45+ in the first half, 46 to 90+ in the second, 0 before kick-off.</summary>
    public static int MatchMinute(int period, int clockMs) => period <= 0 ? 0 : ((period - 1) * 45) + (clockMs / 60_000) + 1;
}
