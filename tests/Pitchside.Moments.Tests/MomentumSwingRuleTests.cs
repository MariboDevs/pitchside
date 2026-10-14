using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class MomentumSwingRuleTests
{
    [Fact]
    public void Crossing_point_seven_upward_is_a_moment_with_the_five_minute_window()
    {
        var swing = new Script().KickOff()
            .Possession().At(120).Pass(Script.Home, "HOM-07", 50, 60).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Moments().Single(MomentTypes.MomentumSwing);

        swing.TriggerSeq.ShouldBe(3);
        swing.SideId.ShouldBe(Script.Home);
        swing.Window.ShouldBe(new MomentWindow(1, 3));
        swing.Evidence.EventIds.ShouldBe([Script.Id(3)]);
        swing.Evidence.FactKeys.ShouldBe(["match.momentum.home", "match.momentum.away", "home.shots", "home.finalThirdEntries", "home.pressingIntensity"]);
        swing.Headline.ShouldBe("Momentum swings to Home Test");
    }

    [Fact]
    public void A_side_swings_at_most_once_per_five_minutes()
    {
        // Home takes momentum, away takes it back, home crosses again inside five minutes: no second home swing.
        var moments = new Script().KickOff()
            .Possession().At(120).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Possession().At(220).Shot(Script.Away, "AWY-10", 0.25, ShotOutcome.Saved).Shot(Script.Away, "AWY-10", 0.25, ShotOutcome.Saved)
            .Possession().At(320).Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved).Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved)
                .Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved).Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved)
                .Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved)
            .Moments();

        moments.Where(m => m.Type == MomentTypes.MomentumSwing).Select(m => m.SideId).ShouldBe([Script.Home, Script.Away]);
    }

    [Fact]
    public void The_same_crossing_after_five_minutes_is_a_new_swing()
    {
        var moments = new Script().KickOff()
            .Possession().At(120).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Possession().At(220).Shot(Script.Away, "AWY-10", 0.25, ShotOutcome.Saved).Shot(Script.Away, "AWY-10", 0.25, ShotOutcome.Saved)
            .Possession().At(430).Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved).Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved)
                .Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved).Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved)
                .Shot(Script.Home, "HOM-09", 0.25, ShotOutcome.Saved)
            .Moments();

        moments.Where(m => m.Type == MomentTypes.MomentumSwing).Select(m => m.SideId).ShouldBe([Script.Home, Script.Away, Script.Home]);
    }

    [Fact]
    public void Staying_above_the_threshold_is_not_a_new_swing()
    {
        new Script().KickOff()
            .Possession().At(120).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Possession().At(400).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Moments().Count(m => m.Type == MomentTypes.MomentumSwing).ShouldBe(1);
    }
}
