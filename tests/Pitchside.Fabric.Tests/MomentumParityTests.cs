using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Fabric.Tests;

/// <summary>
/// The dashboard's momentum (fabric/kql/queries/05-momentum.kql) must be the overlay's (match.momentum.* from
/// Pitchside.Stats). The C# weights are read by running <see cref="EventRules.MomentumWeight"/> on probe events, so a
/// change to the rule's logic, not only to a literal, fails here; the KQL weights are read from the file's constants.
/// </summary>
public sealed class MomentumParityTests
{
    private const string Momentum = "fabric/kql/queries/05-momentum.kql";

    private static readonly IReadOnlyDictionary<string, double> Kql = KqlFile.Constants(Momentum);

    [Fact]
    public void The_kql_declares_exactly_the_weights_this_test_checks() =>
        Kql.Keys.Order(StringComparer.Ordinal).ShouldBe(
            ["cornerWeight", "finalThirdEntryWeight", "finalThirdX", "onTargetWeight", "shotWeight", "windowMs", "xgWeight"]);

    [Fact]
    public void A_shot_weighs_the_same()
    {
        var offTarget = EventRules.MomentumWeight(Shot(onTarget: false, xg: 0));
        var onTarget = EventRules.MomentumWeight(Shot(onTarget: true, xg: 0));
        var withXg = EventRules.MomentumWeight(Shot(onTarget: false, xg: 0.5));

        Kql["shotWeight"].ShouldBe(offTarget);
        Kql["onTargetWeight"].ShouldBe(onTarget - offTarget);
        Kql["xgWeight"].ShouldBe((withXg - offTarget) / 0.5, 1e-9);
    }

    [Fact]
    public void A_corner_weighs_the_same() =>
        Kql["cornerWeight"].ShouldBe(EventRules.MomentumWeight(Event(EventType.Corner, 104.5, 0.5)));

    [Fact]
    public void A_final_third_entry_weighs_the_same_and_starts_at_the_same_line()
    {
        Kql["finalThirdX"].ShouldBe(EventRules.FinalThirdX);
        var line = EventRules.FinalThirdX;

        Kql["finalThirdEntryWeight"].ShouldBe(EventRules.MomentumWeight(Carry(line, line + 1)));
        EventRules.MomentumWeight(Pass(line, line + 1, completed: true)).ShouldBe(Kql["finalThirdEntryWeight"]);
        EventRules.MomentumWeight(Pass(line, line + 1, completed: false)).ShouldBe(0);
        EventRules.MomentumWeight(Carry(line + 0.5, line + 10)).ShouldBe(0);
        EventRules.MomentumWeight(Carry(line - 10, line)).ShouldBe(0);
    }

    [Fact]
    public void No_other_event_type_carries_weight()
    {
        foreach (var type in Enum.GetValues<EventType>().Where(t => t is not (EventType.Shot or EventType.Corner or EventType.Pass or EventType.Carry)))
        {
            EventRules.MomentumWeight(Event(type, 90, 34) with { EndLocation = new Location(95, 34), Completed = true }).ShouldBe(0, type.ToString());
        }
    }

    [Fact]
    public void The_window_is_the_same() => Kql["windowMs"].ShouldBe(EventRules.FiveMinutesMs);

    [Fact]
    public void The_pressing_window_is_the_same() =>
        KqlFile.Constants("fabric/kql/queries/04-pressing-intensity.kql")["windowMs"].ShouldBe(EventRules.TenMinutesMs);

    private static MatchEvent Shot(bool onTarget, double xg) => Event(EventType.Shot, 95, 34) with { OnTarget = onTarget, Xg = xg };

    private static MatchEvent Carry(double fromX, double toX) => Event(EventType.Carry, fromX, 34) with { EndLocation = new Location(toX, 34) };

    private static MatchEvent Pass(double fromX, double toX, bool completed) =>
        Event(EventType.Pass, fromX, 34) with { EndLocation = new Location(toX, 34), Completed = completed };

    private static MatchEvent Event(EventType type, double x, double y) => new()
    {
        Id = "MER-1-00001",
        MatchId = "MER-1",
        Seq = 1,
        Period = 1,
        ClockMs = 60_000,
        WallTimeUtc = new DateTimeOffset(2026, 10, 10, 15, 1, 0, TimeSpan.Zero),
        Type = type,
        TeamId = "HOM",
        PlayerId = "HOM-07",
        Location = new Location(x, y),
        PossessionId = 1,
    };
}
