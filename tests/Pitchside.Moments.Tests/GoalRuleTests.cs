using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Tests;

public sealed class GoalRuleTests
{
    [Fact]
    public void A_goal_is_a_moment_over_the_possession_that_scored()
    {
        var script = new Script().KickOff()
            .Possession().At(5).Pass(Script.Home, "HOM-07", 50, 75).Pass(Script.Home, "HOM-09", 75, 92)
            .Scores(Script.Home, "HOM-07", 0.42, assist: "HOM-09");

        var goal = script.Moments().Single(MomentTypes.Goal);

        goal.Id.ShouldBe("MER-1:M:0005:goal");
        goal.Priority.ShouldBe(100);
        goal.TriggerSeq.ShouldBe(5);
        goal.Window.ShouldBe(new MomentWindow(2, 5));
        goal.SideId.ShouldBe(Script.Home);
        goal.PlayerIds.ShouldBe(["HOM-07", "HOM-09"]);
        goal.Evidence.EventIds.ShouldBe([Script.Id(2), Script.Id(4), Script.Id(5)]);
        goal.Evidence.FactKeys.ShouldBe(
        [
            FactKeys.MatchScoreHome, FactKeys.MatchScoreAway, "player.HOM-07.goals", "player.HOM-09.assists",
            "home.xg", FactKeys.MatchMomentumHome, FactKeys.MatchMomentumAway,
        ]);
        goal.Headline.ShouldBe("Goal: Home Test 1-0");
        goal.FactSheetSeq.ShouldBe(5);
    }

    [Fact]
    public void Every_goal_is_a_moment_however_close_together()
    {
        var moments = new Script().KickOff()
            .Possession().At(10).Scores(Script.Home, "HOM-07")
            .Possession().At(20).Scores(Script.Away, "AWY-10")
            .Moments();

        moments.Where(m => m.Type == MomentTypes.Goal).Select(m => m.Headline).ShouldBe(["Goal: Home Test 1-0", "Goal: Away Test 1-1"]);
    }
}
