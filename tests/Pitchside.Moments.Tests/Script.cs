using System.Globalization;
using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

/// <summary>
/// Writes a hand-made event list one event at a time, with sequence numbers, ids and possessions filled in, so
/// each rule's test reads like the passage of play it describes. Home is HOM, away is AWY, three players a side.
/// </summary>
internal sealed class Script
{
    public const string Home = "HOM";
    public const string Away = "AWY";

    private readonly List<MatchEvent> _events = [];
    private int _period = 1;
    private int _clockMs;
    private int _possession;
    private int _homeGoals;
    private int _awayGoals;

    public static MatchDocument Document { get; } = new(
        SchemaVersion.Current,
        "MER-1",
        1,
        "Meridian League",
        "Test Ground",
        new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero),
        Sheet(Home, "Home Test", ["HOM-01", "HOM-07", "HOM-09"], ["HOM-12"]),
        Sheet(Away, "Away Test", ["AWY-01", "AWY-05", "AWY-10"], ["AWY-14"]));

    public IReadOnlyList<MatchEvent> Events => _events;

    public static string Id(int seq) => string.Create(CultureInfo.InvariantCulture, $"MER-1-{seq:00000}");

    /// <summary>Moves the period clock to <paramref name="seconds"/>.</summary>
    public Script At(double seconds)
    {
        _clockMs = (int)(seconds * 1000);
        return this;
    }

    /// <summary>Starts a new possession for the next events.</summary>
    public Script Possession()
    {
        _possession++;
        return this;
    }

    public Script KickOff(string team = Home)
    {
        if (_events.Count > 0)
        {
            _period = 2;
            _clockMs = 0;
        }

        return Add(EventType.KickOff, team, team + "-07", 52.5, 34);
    }

    public Script Pass(string team, string player, double fromX, double toX, double difficulty = 0.2, bool completed = true) =>
        Add(EventType.Pass, team, player, fromX, 34, e => e with
        {
            EndLocation = new Location(toX, 34),
            LengthM = Math.Abs(toX - fromX),
            Difficulty = difficulty,
            Completed = completed,
        });

    public Script Shot(string team, string player, double xg, ShotOutcome outcome = ShotOutcome.Saved, bool penalty = false, string? assist = null) =>
        Add(EventType.Shot, team, player, penalty ? 94 : 96, 34, e => e with
        {
            Xg = xg,
            OnTarget = outcome is ShotOutcome.Goal or ShotOutcome.Saved,
            Outcome = outcome,
            ShotSpeedKph = 80,
            BodyPart = BodyPart.RightFoot,
            Penalty = penalty ? true : null,
            AssistId = outcome == ShotOutcome.Goal ? assist : null,
        });

    public Script Goal(string team, string player, string? assist = null)
    {
        if (team == Home)
        {
            _homeGoals++;
        }
        else
        {
            _awayGoals++;
        }

        return Add(EventType.Goal, team, player, 96, 34, e => e with { AssistId = assist, HomeScore = _homeGoals, AwayScore = _awayGoals });
    }

    /// <summary>A shot that scores followed by its goal event.</summary>
    public Script Scores(string team, string player, double xg = 0.3, string? assist = null) =>
        Shot(team, player, xg, ShotOutcome.Goal, assist: assist).Goal(team, player, assist);

    public Script Corner(string team, string player) => Add(EventType.Corner, team, player, 104.5, 0.5);

    public Script Foul(string team, string player, double x = 40) => Add(EventType.Foul, team, player, x, 34, e => e with { CardAwarded = true });

    public Script Card(string team, string player, CardColour colour, bool secondYellow = false) =>
        Add(EventType.Card, team, player, 40, 34, e => e with { Colour = colour, SecondYellow = secondYellow });

    public Script Turnover(string team, string player) => Add(EventType.Turnover, team, player, 60, 34, e => e with { Reason = TurnoverReason.Tackle });

    public Script Tracking(string team, params TrackingEntry[] entries) => Add(EventType.Tracking, team, null, 52.5, 34, e => e with { Tracking = entries });

    public Script HalfEnd() => Add(EventType.HalfEnd, Home, null, 52.5, 34);

    public Script FullTime() => Add(EventType.FullTime, Home, null, 52.5, 34, e => e with { HomeScore = _homeGoals, AwayScore = _awayGoals });

    public IReadOnlyList<Moment> Moments() => MatchMoments.Detect(Document, _events);

    private Script Add(EventType type, string team, string? player, double x, double y, Func<MatchEvent, MatchEvent>? qualify = null)
    {
        var seq = _events.Count + 1;
        var e = new MatchEvent
        {
            Id = Id(seq),
            MatchId = "MER-1",
            Seq = seq,
            Period = _period,
            ClockMs = _clockMs,
            WallTimeUtc = Document.KickoffUtc.AddMilliseconds(_clockMs + (_period == 2 ? 3_600_000 : 0)),
            Type = type,
            TeamId = team,
            PlayerId = player,
            Location = new Location(x, y),
            PossessionId = type == EventType.KickOff && _period == 1 ? 0 : _possession,
        };
        _events.Add(qualify is null ? e : qualify(e));
        _clockMs += 1000;
        return this;
    }

    private static TeamSheet Sheet(string id, string name, string[] starters, string[] bench) => new(
        new Club(id, name, id, "#112233", "#445566"),
        "4-3-3",
        [.. starters.Select((p, i) => new Player(p, $"Player {p}", int.Parse(p[4..], CultureInfo.InvariantCulture), i == 0 ? Position.Goalkeeper : Position.Forward, 110))],
        [.. bench.Select(p => new Player(p, $"Player {p}", int.Parse(p[4..], CultureInfo.InvariantCulture), Position.Midfielder, 110))]);
}
