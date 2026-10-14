using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class BigChanceMissedRuleTests
{
    [Fact]
    public void A_missed_shot_at_or_above_point_three_is_a_moment()
    {
        var miss = new Script().KickOff()
            .Possession().At(100).Pass(Script.Away, "AWY-05", 60, 90).Shot(Script.Away, "AWY-10", 0.30, ShotOutcome.OffTarget)
            .Moments().Single(MomentTypes.BigChanceMissed);

        miss.Window.ShouldBe(new MomentWindow(2, 3));
        miss.PlayerIds.ShouldBe(["AWY-10"]);
        miss.Evidence.FactKeys.ShouldContain("away.bigChances");
        miss.Headline.ShouldBe("Big chance missed: Player AWY-10, Away Test");
    }

    [Theory]
    [InlineData(0.29, ShotOutcome.Saved)]
    [InlineData(0.6, ShotOutcome.Goal)]
    public void A_small_chance_or_a_goal_is_not(double xg, ShotOutcome outcome) =>
        new Script().KickOff().Possession().At(100).Shot(Script.Away, "AWY-10", xg, outcome)
            .Moments().ShouldHaveNone(MomentTypes.BigChanceMissed);
}
