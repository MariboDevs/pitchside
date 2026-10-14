using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>
/// A player's second goal (brace) or third (hat-trick), or, at the first event of minute 80, a clean sheet still
/// intact. When both sides keep one, a single moment covers both, so the derivable id stays unique.
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
        if (e.Type == EventType.Goal && e.PlayerId is { } scorer)
        {
            var goals = (int)MomentBuilder.Number(context, FactKeys.ForPlayer(scorer, FactKeys.Goals));
            if (goals is 2 or 3)
            {
                var player = state.PlayerName(scorer);
                var club = state.ClubName(e.TeamId);
                return MomentBuilder.Create(
                    context, Type, Priority, e.Seq, e.TeamId,
                    [scorer],
                    [e.Id],
                    [FactKeys.ForPlayer(scorer, FactKeys.Goals), FactKeys.MatchScoreHome, FactKeys.MatchScoreAway, FactKeys.MatchClockMinute],
                    goals == 2 ? MomentHeadlines.Brace(player, club) : MomentHeadlines.HatTrick(player, club));
            }
        }

        if (context.Previous.CleanSheetChecked || e.Period != 2
            || MomentBuilder.Number(context, FactKeys.MatchClockMinute) < CleanSheetMinute)
        {
            return null;
        }

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

        return MomentBuilder.Create(
            context, Type, Priority, e.Seq, keepers.Count == 1 ? keepers[0] : null,
            [],
            [e.Id],
            [FactKeys.MatchScoreHome, FactKeys.MatchScoreAway, FactKeys.MatchClockMinute],
            keepers.Count == 1 ? MomentHeadlines.CleanSheet(state.ClubName(keepers[0])) : MomentHeadlines.CleanSheets());
    }
}
