using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class PassTagRuleTests
{
    [Fact]
    public void A_hard_pass_that_leads_to_a_shot_is_a_moment_on_the_shot()
    {
        // Home already holds the momentum, so the shot is not a new swing that would outrank the tag.
        var tag = new Script().KickOff()
            .Possession().At(10).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Possession().At(200).Pass(Script.Home, "HOM-07", 72, 88, difficulty: 0.65).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Moments().Single(MomentTypes.PassTag);

        tag.TriggerSeq.ShouldBe(4);
        tag.Window.ShouldBe(new MomentWindow(3, 4));
        tag.PlayerIds.ShouldBe(["HOM-07", "HOM-09"]);
        tag.Evidence.EventIds.ShouldBe([Script.Id(3), Script.Id(4)]);
        tag.Headline.ShouldBe("Hard pass from Player HOM-07 leads to a shot");
    }

    [Fact]
    public void A_hard_pass_in_a_possession_that_ends_without_a_shot_is_not()
    {
        new Script().KickOff()
            .Possession().At(100).Pass(Script.Home, "HOM-07", 60, 88, difficulty: 0.65).Turnover(Script.Home, "HOM-09")
            .Possession().At(130).Shot(Script.Away, "AWY-10", 0.05, ShotOutcome.OffTarget)
            .Moments().ShouldHaveNone(MomentTypes.PassTag);
    }

    [Fact]
    public void An_incomplete_hard_pass_raises_no_tag()
    {
        new Script().KickOff()
            .Possession().At(100).Pass(Script.Home, "HOM-07", 60, 88, difficulty: 0.65, completed: false).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Moments().ShouldHaveNone(MomentTypes.PassTag);
    }
}
