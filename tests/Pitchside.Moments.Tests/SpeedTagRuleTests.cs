using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class SpeedTagRuleTests
{
    [Fact]
    public void Reaching_thirty_three_kph_is_a_moment()
    {
        var speed = new Script().KickOff().Possession()
            .At(60).Tracking(Script.Away, new TrackingEntry("AWY-10", 110, 33.4), new TrackingEntry("AWY-05", 110, 20))
            .Moments().Single(MomentTypes.SpeedTag);

        speed.PlayerIds.ShouldBe(["AWY-10"]);
        speed.SideId.ShouldBe(Script.Away);
        speed.Window.ShouldBe(new MomentWindow(2, 2));
        speed.Evidence.FactKeys.ShouldBe(["player.AWY-10.topSpeedKph", "player.AWY-10.sprints"]);
        speed.Headline.ShouldBe("Speed: Player AWY-10 passes 33 kph");
    }

    [Fact]
    public void Reaching_ten_kilometres_is_a_moment()
    {
        var distance = new Script().KickOff().Possession()
            .At(60).Tracking(Script.Home, new TrackingEntry("HOM-07", 10_000, 20))
            .Moments().Single(MomentTypes.SpeedTag);

        distance.Evidence.FactKeys.ShouldBe(["player.HOM-07.distanceKm", "player.HOM-07.sprints"]);
        distance.Headline.ShouldBe("Distance: Player HOM-07 passes 10 km");
    }

    [Fact]
    public void Thirty_one_kph_is_not()
    {
        new Script().KickOff().Possession().At(60).Tracking(Script.Away, new TrackingEntry("AWY-10", 110, 31))
            .Moments().ShouldHaveNone(MomentTypes.SpeedTag);
    }
}
