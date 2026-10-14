using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

/// <summary>Kick-off, half time and full time.</summary>
public sealed class PeriodRuleTests
{
    private static IReadOnlyList<Moment> WholeMatch() => new Script().KickOff()
        .Possession().At(100).Scores(Script.Home, "HOM-09", assist: "HOM-07")
        .At(2_760).HalfEnd()
        .KickOff(Script.Away).Possession().At(600).Scores(Script.Away, "AWY-10")
        .At(2_880).FullTime()
        .Moments();

    [Fact]
    public void The_opening_kick_off_is_a_moment_with_no_fact_keys()
    {
        var kickOff = WholeMatch().Single(MomentTypes.KickOff);
        kickOff.Id.ShouldBe("MER-1:M:0001:kick_off");
        kickOff.Priority.ShouldBe(20);
        kickOff.Evidence.EventIds.ShouldBe([Script.Id(1)]);
        kickOff.Evidence.FactKeys.ShouldBeEmpty();
        kickOff.Headline.ShouldBe("Kick-off: Home Test v Away Test");
    }

    [Fact]
    public void Half_time_spans_the_first_half()
    {
        var halfTime = WholeMatch().Single(MomentTypes.HalfTime);
        halfTime.Window.ShouldBe(new MomentWindow(1, 4));
        halfTime.Priority.ShouldBe(80);
        halfTime.Evidence.EventIds.ShouldBe([Script.Id(3), Script.Id(4)]);
        halfTime.Evidence.FactKeys.Count.ShouldBe(10);
        halfTime.Headline.ShouldBe("Half time: Home Test 1-0 Away Test");
    }

    [Fact]
    public void Full_time_spans_the_match_and_names_its_top_players()
    {
        var fullTime = WholeMatch().Single(MomentTypes.FullTime);
        fullTime.Window.ShouldBe(new MomentWindow(1, 8));
        fullTime.Evidence.EventIds.ShouldBe([Script.Id(3), Script.Id(7), Script.Id(8)]);
        fullTime.Evidence.FactKeys.ShouldContain("home.passCompletion");
        fullTime.Evidence.FactKeys.ShouldContain("player.AWY-10.goals");
        fullTime.Headline.ShouldBe("Full time: Home Test 1-1 Away Test");
    }
}
