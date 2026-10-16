using Pitchside.Contracts;

namespace Pitchside.Moments.Demo;

/// <summary>
/// Scores a match for drama (WP-11): three to five goals, a lead that changes hands, a red card or a penalty, a big
/// chance missed, and a momentum swing in each half. Everything is read from the events and the detected moments.
/// </summary>
public static class DemoSeedScorer
{
    public const int MinGoals = 3;
    public const int MaxGoals = 5;

    public static DemoSeedScore Score(long seed, MatchDocument document, IReadOnlyList<MatchEvent> events, IReadOnlyList<Moment> moments)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(moments);
        var goals = events.Where(e => e.Type == EventType.Goal).OrderBy(e => e.Seq).Select(e => e.TeamId == document.Home.Club.Id).ToList();
        return new DemoSeedScore(
            seed,
            goals.Count,
            goals.Count is >= MinGoals and <= MaxGoals,
            LeadChanges(goals) > 0,
            moments.Any(m => m.Type is MomentTypes.RedCard or MomentTypes.Penalty),
            moments.Any(m => m.Type == MomentTypes.BigChanceMissed),
            moments.Any(m => m.Type == MomentTypes.MomentumSwing && m.Period == 1) && moments.Any(m => m.Type == MomentTypes.MomentumSwing && m.Period == 2));
    }

    /// <summary>
    /// How many times the lead passed from one side to the other, given the goals in order (true for a home goal). A
    /// side drawing level is not a change; the next goal for the other side is.
    /// </summary>
    public static int LeadChanges(IEnumerable<bool> homeGoals)
    {
        ArgumentNullException.ThrowIfNull(homeGoals);
        var margin = 0;
        var leader = 0;
        var changes = 0;
        foreach (var home in homeGoals)
        {
            margin += home ? 1 : -1;
            var now = Math.Sign(margin);
            if (now != 0 && leader != 0 && now != leader)
            {
                changes++;
            }

            if (now != 0)
            {
                leader = now;
            }
        }

        return changes;
    }

    /// <summary>The best seed of those given: highest total, then the lowest seed.</summary>
    public static DemoSeedScore Best(IEnumerable<DemoSeedScore> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        return scores.OrderByDescending(s => s.Total).ThenBy(s => s.Seed).First();
    }
}
