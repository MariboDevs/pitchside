using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class PenaltyRuleTests
{
    [Fact]
    public void A_penalty_spans_the_foul_to_the_shot()
    {
        var moments = new Script().KickOff()
            .Possession().At(30).Pass(Script.Home, "HOM-07", 70, 90).Foul(Script.Away, "AWY-05", 12)
            .Possession().At(60).Shot(Script.Home, "HOM-09", 0.76, ShotOutcome.Saved, penalty: true)
            .Moments();

        var penalty = moments.Single(MomentTypes.Penalty);
        penalty.Window.ShouldBe(new MomentWindow(3, 4));
        penalty.Evidence.EventIds.ShouldBe([Script.Id(3), Script.Id(4)]);
        penalty.Evidence.FactKeys.ShouldBe(["home.xg", "player.HOM-09.xg", "match.score.home", "match.score.away"]);
        penalty.Headline.ShouldBe("Penalty to Home Test");

        // A missed penalty is not also a big chance missed.
        moments.ShouldHaveNone(MomentTypes.BigChanceMissed);
    }
}
