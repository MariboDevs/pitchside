using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments;

/// <summary>Builds a moment for the event in a rule context, with the derivable id and the sheet's sequence.</summary>
public static class MomentBuilder
{
    public static Moment Create(
        RuleContext context,
        string type,
        int priority,
        int fromSeq,
        string? sideId,
        IEnumerable<string?> playerIds,
        IEnumerable<string> eventIds,
        IEnumerable<string> factKeys,
        string headline)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        return new Moment
        {
            Id = Moment.IdFor(e.MatchId, e.Seq, type),
            MatchId = e.MatchId,
            Type = type,
            Priority = priority,
            Period = e.Period,
            ClockMs = e.ClockMs,
            TriggerSeq = e.Seq,
            Window = new MomentWindow(Math.Min(fromSeq, e.Seq), e.Seq),
            SideId = sideId,
            PlayerIds = [.. playerIds.OfType<string>().Distinct(StringComparer.Ordinal)],
            Evidence = new MomentEvidence(
                [.. eventIds.Distinct(StringComparer.Ordinal)],
                [.. factKeys.Distinct(StringComparer.Ordinal)]),
            Headline = headline,
            FactSheetSeq = context.Sheet.AsOfSeq,
        };
    }

    /// <summary>The home and away score as of the context's sheet.</summary>
    public static (int Home, int Away) Score(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ((int)(context.Sheet.Find(FactKeys.MatchScoreHome)?.Value.Number ?? 0),
            (int)(context.Sheet.Find(FactKeys.MatchScoreAway)?.Value.Number ?? 0));
    }

    public static double Number(RuleContext context, string key)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Sheet.Find(key)?.Value.Number ?? 0;
    }

    /// <summary>Events of the current period at or after a clock, from the five-minute window, oldest first.</summary>
    public static IReadOnlyList<MatchEvent> Since(RuleContext context, int fromClockMs)
    {
        ArgumentNullException.ThrowIfNull(context);
        return [.. context.Current.Recent.Since(fromClockMs)];
    }
}
