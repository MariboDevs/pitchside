using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class MilestoneRuleTests
{
    [Fact]
    public void A_second_and_a_third_goal_are_a_brace_and_a_hat_trick()
    {
        var milestones = new Script().KickOff()
            .Possession().At(100).Scores(Script.Home, "HOM-09")
            .Possession().At(300).Scores(Script.Home, "HOM-09")
            .Possession().At(500).Scores(Script.Home, "HOM-09")
            .Moments().Where(m => m.Type == MomentTypes.Milestone).ToList();

        milestones.Select(m => m.Headline).ShouldBe(["Brace: Player HOM-09, Home Test", "Hat-trick: Player HOM-09, Home Test"]);
        milestones[0].Evidence.FactKeys.ShouldBe(["player.HOM-09.goals", "match.score.home", "match.score.away", "match.clock.minute"]);
        milestones[0].Window.FromSeq.ShouldBe(milestones[0].TriggerSeq);
    }

    [Fact]
    public void A_clean_sheet_still_intact_at_minute_eighty_is_a_moment_once()
    {
        var moments = new Script().KickOff()
            .Possession().At(100).Scores(Script.Home, "HOM-09")
            .At(2_700).HalfEnd()
            .KickOff(Script.Away).Possession().At(2_000).Turnover(Script.Away, "AWY-05")
            .At(2_100).Turnover(Script.Home, "HOM-07").At(2_200).Turnover(Script.Away, "AWY-05")
            .Moments();

        var clean = moments.Single(MomentTypes.Milestone);
        clean.TriggerSeq.ShouldBe(7);
        clean.SideId.ShouldBe(Script.Home);
        clean.Headline.ShouldBe("Clean sheet intact at 80 minutes: Home Test");
    }

    [Fact]
    public void Both_clean_sheets_share_one_moment()
    {
        new Script().KickOff().At(2_700).HalfEnd().KickOff(Script.Away).Possession().At(2_100).Turnover(Script.Away, "AWY-05")
            .Moments().Single(MomentTypes.Milestone).SideId.ShouldBeNull();
    }
}
