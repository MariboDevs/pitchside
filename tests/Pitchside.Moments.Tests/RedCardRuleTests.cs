using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class RedCardRuleTests
{
    [Fact]
    public void A_red_card_spans_the_foul_and_the_card()
    {
        var red = new Script().KickOff()
            .Possession().At(30).Pass(Script.Home, "HOM-07", 50, 60).Foul(Script.Away, "AWY-05").Card(Script.Away, "AWY-05", CardColour.Red)
            .Moments().Single(MomentTypes.RedCard);

        red.Window.ShouldBe(new MomentWindow(3, 4));
        red.SideId.ShouldBe(Script.Away);
        red.Evidence.EventIds.ShouldBe([Script.Id(3), Script.Id(4)]);
        red.Evidence.FactKeys.ShouldBe(["away.reds", "away.fouls", "player.AWY-05.cards"]);
        red.Headline.ShouldBe("Red card: Player AWY-05, Away Test");
    }

    [Fact]
    public void A_yellow_card_is_not_a_moment()
    {
        new Script().KickOff()
            .Possession().At(30).Foul(Script.Away, "AWY-05").Card(Script.Away, "AWY-05", CardColour.Yellow)
            .Moments().ShouldHaveNone(MomentTypes.RedCard);
    }
}
