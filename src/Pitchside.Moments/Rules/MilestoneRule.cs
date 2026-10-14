using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>
/// A player's second goal (brace) or third (hat-trick), or, at the first second-half event of minute 80, a clean
/// sheet still intact. When both sides keep one, or a brace lands on that same event, a single moment carries
/// both, so the derivable id stays unique and the rate limit cannot drop either.
/// </summary>
public sealed class MilestoneRule : IMomentRule
{
    public const int CleanSheetMinute = 80;

    public string Type => MomentTypes.Milestone;

    public int Priority => MomentTypes.MilestonePriority;

    public Moment? Evaluate(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var e = context.Event;
        var state = context.Current;
        var parts = new List<(string? Side, string? Player, IReadOnlyList<string> Keys, string Headline)>();

        if (e.Type == EventType.Goal && e.PlayerId is { } scorer)
        {
            var goals = (int)MomentBuilder.Number(context, FactKeys.ForPlayer(scorer, FactKeys.Goals));
            if (goals is 2 or 3)
            {
                var player = state.PlayerName(scorer);
                var club = state.ClubName(e.TeamId);
                parts.Add((e.TeamId, scorer,
                    [FactKeys.ForPlayer(scorer, FactKeys.Goals), FactKeys.MatchScoreHome, FactKeys.MatchScoreAway, FactKeys.MatchClockMinute],
                    goals == 2 ? MomentHeadlines.Brace(player, club) : MomentHeadlines.HatTrick(player, club)));
            }
        }

        if (CleanSheetDue(context) && CleanSheet(context) is { } cleanSheet)
        {
            parts.Add(cleanSheet);
        }

        if (parts.Count == 0)
        {
            return null;
        }

        // A brace on the very event the clean-sheet check falls on is one moment carrying both, so neither is lost to
        // the derivable id or the rate limit.
        var sides = parts.Select(p => p.Side).Distinct().ToList();
        return MomentBuilder.Create(
            context, Type, Priority, e.Seq, sides.Count == 1 ? sides[0] : null,
            parts.Select(p => p.Player),
            [e.Id],
            parts.SelectMany(p => p.Keys),
            string.Join(". ", parts.Select(p => p.Headline)));
    }

    /// <summary>True on the first second-half event at or after minute 80.</summary>
    public static bool CleanSheetDue(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return !context.Previous.CleanSheetChecked && context.Event.Period == 2
            && MomentBuilder.Number(context, FactKeys.MatchClockMinute) >= CleanSheetMinute;
    }

    private static (string? Side, string? Player, IReadOnlyList<string> Keys, string Headline)? CleanSheet(RuleContext context)
    {
        var state = context.Current;
        var (home, away) = MomentBuilder.Score(context);
        var keepers = new List<string>();
        if (away == 0)
        {
            keepers.Add(state.HomeId);
        }

        if (home == 0)
        {
            keepers.Add(state.AwayId);
        }

        if (keepers.Count == 0)
        {
            return null;
        }

        return (keepers.Count == 1 ? keepers[0] : null, null,
            [FactKeys.MatchScoreHome, FactKeys.MatchScoreAway, FactKeys.MatchClockMinute],
            keepers.Count == 1 ? MomentHeadlines.CleanSheet(state.ClubName(keepers[0])) : MomentHeadlines.CleanSheets());
    }
}
