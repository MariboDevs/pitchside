using Pitchside.Engine;
using Pitchside.Moments.Demo;

namespace Pitchside.Moments.Tests;

public sealed class DemoSeedScorerTests
{
    [Theory]
    [InlineData(new bool[0], 0)]
    [InlineData(new[] { true, true, false }, 0)]
    [InlineData(new[] { true, false, false }, 1)]
    [InlineData(new[] { false, true, true, false, false }, 2)]
    [InlineData(new[] { true, false, true, false }, 0)]
    public void The_lead_changes_only_when_the_other_side_goes_ahead(bool[] homeGoals, int changes) =>
        DemoSeedScorer.LeadChanges(homeGoals).ShouldBe(changes);

    [Fact]
    public void A_seed_is_scored_from_its_own_events_and_moments()
    {
        var match = MatchSimulator.Simulate(7);
        var moments = MatchMoments.Detect(match.Document, match.Events);

        var score = DemoSeedScorer.Score(7, match.Document, match.Events, moments);

        score.Goals.ShouldBe(match.HomeScore + match.AwayScore);
        score.GoalsInRange.ShouldBe(score.Goals is >= DemoSeedScorer.MinGoals and <= DemoSeedScorer.MaxGoals);
        score.BigChanceMissed.ShouldBe(moments.Any(m => m.Type == MomentTypes.BigChanceMissed));
        score.Total.ShouldBeInRange(0, 5);
    }

    [Fact]
    public void The_best_seed_has_the_highest_total_and_the_lowest_seed_breaks_a_tie()
    {
        DemoSeedScore Of(long seed, bool all) => new(seed, 4, all, all, all, all, all);

        DemoSeedScorer.Best([Of(9, false), Of(5, true), Of(3, true)]).Seed.ShouldBe(3);
    }
}
