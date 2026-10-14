using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class SustainedPressureRuleTests
{
    [Fact]
    public void Three_shots_in_three_minutes_without_scoring_is_a_moment()
    {
        var moments = new Script().KickOff()
            .Possession().At(10).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Possession().At(105).Shot(Script.Home, "HOM-07", 0.05, ShotOutcome.Blocked)
            .Possession().At(185).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Moments();

        var pressure = moments.Single(MomentTypes.SustainedPressure);
        pressure.TriggerSeq.ShouldBe(4);
        pressure.Window.ShouldBe(new MomentWindow(2, 4));
        pressure.Evidence.EventIds.ShouldBe([Script.Id(2), Script.Id(3), Script.Id(4)]);
        pressure.Evidence.FactKeys.ShouldBe(["home.shots", "home.corners", "home.finalThirdEntries", "home.possessionShare"]);
        pressure.Headline.ShouldBe("Home Test keep up the pressure");
    }

    [Fact]
    public void Two_corners_in_three_minutes_is_a_moment()
    {
        new Script().KickOff()
            .Possession().At(100).Corner(Script.Away, "AWY-05")
            .Possession().At(200).Corner(Script.Away, "AWY-05")
            .Moments().Single(MomentTypes.SustainedPressure).SideId.ShouldBe(Script.Away);
    }

    [Fact]
    public void Scoring_in_the_window_cancels_it()
    {
        new Script().KickOff()
            .Possession().At(10).Scores(Script.Home, "HOM-09", 0.05)
            .Possession().At(105).Shot(Script.Home, "HOM-07", 0.05, ShotOutcome.Blocked)
            .Possession().At(185).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Moments().ShouldHaveNone(MomentTypes.SustainedPressure);
    }

    [Fact]
    public void Shots_spread_over_more_than_three_minutes_are_not_pressure()
    {
        new Script().KickOff()
            .Possession().At(10).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Possession().At(105).Shot(Script.Home, "HOM-07", 0.05, ShotOutcome.Blocked)
            .Possession().At(200).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Moments().ShouldHaveNone(MomentTypes.SustainedPressure);
    }
}
