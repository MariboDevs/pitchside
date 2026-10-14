using Pitchside.Contracts;

namespace Pitchside.Stats.Tests;

/// <summary>
/// A hand-written 20-event match between two three-player sides, small enough to compute every metric by hand.
/// Home scores once in the first half; the expected values in <see cref="MetricDefinitionTests"/> are worked out
/// from this list alone.
/// </summary>
internal static class TinyMatch
{
    public const string Home = "HOM";
    public const string Away = "AWY";

    public static MatchDocument Document { get; } = new(
        SchemaVersion.Current,
        "MER-1",
        1,
        "Meridian League",
        "Test Ground",
        new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero),
        Sheet(Home, "Home Test", ["HOM-01", "HOM-07", "HOM-09"], ["HOM-12"]),
        Sheet(Away, "Away Test", ["AWY-01", "AWY-05", "AWY-10"], ["AWY-14"]));

    public static IReadOnlyList<MatchEvent> Events { get; } =
    [
        E(1, 1, 0, EventType.KickOff, Home, "HOM-07", 52.5, 34, 0),
        E(2, 1, 2_000, EventType.Pass, Home, "HOM-07", 52.5, 34, 1) with { EndLocation = new(65, 30), LengthM = 13.1, Difficulty = 0.2, Completed = true, ReceiverId = "HOM-09" },
        E(3, 1, 4_000, EventType.Pressure, Away, "AWY-05", 40, 38, 1) with { Intensity = 0.5 },
        E(4, 1, 4_000, EventType.Carry, Home, "HOM-09", 65, 30, 1) with { EndLocation = new(78, 32), LengthM = 13.2, SpeedKph = 27 },
        E(5, 1, 8_000, EventType.Pass, Home, "HOM-09", 78, 32, 1) with { EndLocation = new(92, 34), LengthM = 14.1, Difficulty = 0.6, Completed = true, ReceiverId = "HOM-07" },
        E(6, 1, 10_000, EventType.Shot, Home, "HOM-07", 95, 34, 1) with { Xg = 0.4, OnTarget = true, Outcome = ShotOutcome.Goal, ShotSpeedKph = 104, BodyPart = BodyPart.RightFoot, AssistId = "HOM-09" },
        E(7, 1, 10_000, EventType.Goal, Home, "HOM-07", 95, 34, 1) with { AssistId = "HOM-09", HomeScore = 1, AwayScore = 0 },
        E(8, 1, 72_000, EventType.Pass, Away, "AWY-10", 52.5, 34, 2) with { EndLocation = new(40, 34), LengthM = 12.5, Difficulty = 0.1, Completed = true, ReceiverId = "AWY-05" },
        E(9, 1, 75_000, EventType.Pass, Away, "AWY-05", 40, 34, 2) with { EndLocation = new(60, 20), LengthM = 24.4, Difficulty = 0.3, Completed = false },
        E(10, 1, 77_000, EventType.Turnover, Away, "AWY-05", 60, 20, 2) with { Reason = TurnoverReason.Interception },
        E(11, 1, 80_000, EventType.Foul, Away, "AWY-05", 50, 40, 3) with { FouledPlayerId = "HOM-09", CardAwarded = true },
        E(12, 1, 80_000, EventType.Card, Away, "AWY-05", 50, 40, 3) with { Colour = CardColour.Yellow, SecondYellow = false },
        E(13, 1, 90_000, EventType.Tracking, Home, null, 52.5, 34, 3) with
        {
            Tracking = [new("HOM-01", 120, 18), new("HOM-07", 130, 31), new("HOM-09", 126, 22)],
        },
        E(14, 1, 90_000, EventType.Tracking, Away, null, 52.5, 34, 3) with
        {
            Tracking = [new("AWY-01", 100, 15), new("AWY-05", 110, 29), new("AWY-10", 116, 34)],
        },
        E(15, 1, 100_000, EventType.Corner, Home, "HOM-09", 104.5, 0.5, 4),
        E(16, 1, 103_000, EventType.Shot, Home, "HOM-09", 100, 30, 4) with { Xg = 0.1, OnTarget = false, Outcome = ShotOutcome.Blocked, ShotSpeedKph = 80, BodyPart = BodyPart.LeftFoot },
        E(17, 1, 2_760_000, EventType.HalfEnd, Home, null, 52.5, 34, 4),
        E(18, 2, 0, EventType.KickOff, Away, "AWY-10", 52.5, 34, 4),
        E(19, 2, 60_000, EventType.Substitution, Home, "HOM-12", 52.5, 0.5, 4) with { PlayerOffId = "HOM-09", PlayerOnId = "HOM-12" },
        E(20, 2, 120_000, EventType.FullTime, Home, null, 52.5, 34, 4) with { HomeScore = 1, AwayScore = 0 },
    ];

    public static string Id(int seq) => $"MER-1-{seq:00000}";

    private static TeamSheet Sheet(string id, string name, string[] starters, string[] bench) => new(
        new Club(id, name, id, "#112233", "#445566"),
        "4-3-3",
        [.. starters.Select((p, i) => new Player(p, p, int.Parse(p[4..], System.Globalization.CultureInfo.InvariantCulture), i == 0 ? Position.Goalkeeper : Position.Forward, 110))],
        [.. bench.Select(p => new Player(p, p, int.Parse(p[4..], System.Globalization.CultureInfo.InvariantCulture), Position.Midfielder, 110))]);

    private static MatchEvent E(int seq, int period, int clockMs, EventType type, string team, string? player, double x, double y, int possession) => new()
    {
        Id = Id(seq),
        MatchId = "MER-1",
        Seq = seq,
        Period = period,
        ClockMs = clockMs,
        WallTimeUtc = Document.KickoffUtc.AddMilliseconds(clockMs + (period == 2 ? 3_660_000 : 0)),
        Type = type,
        TeamId = team,
        PlayerId = player,
        Location = new Location(x, y),
        PossessionId = possession,
    };
}
