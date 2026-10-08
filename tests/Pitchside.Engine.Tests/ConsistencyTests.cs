using Pitchside.Contracts;
using Pitchside.Engine;

namespace Pitchside.Engine.Tests;

public sealed class ConsistencyTests
{
    public static TheoryData<long> Seeds => [3, 11, 42, 99, 1234];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Score_in_full_time_event_equals_goal_events(long seed)
    {
        var match = MatchSimulator.Simulate(seed);
        var fullTime = match.Events.Single(e => e.Type == EventType.FullTime);

        fullTime.HomeScore.ShouldBe(match.HomeScore);
        fullTime.AwayScore.ShouldBe(match.AwayScore);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Every_goal_follows_a_scoring_shot_by_the_same_player(long seed)
    {
        var match = MatchSimulator.Simulate(seed);
        var events = match.Events;
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Type != EventType.Goal)
            {
                continue;
            }

            var shot = events[i - 1];
            shot.Type.ShouldBe(EventType.Shot);
            shot.Outcome.ShouldBe(ShotOutcome.Goal);
            shot.PlayerId.ShouldBe(events[i].PlayerId);
            shot.TeamId.ShouldBe(events[i].TeamId);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Sequence_numbers_clock_and_periods_are_monotonic(long seed)
    {
        var match = MatchSimulator.Simulate(seed);
        var previous = match.Events[0];
        foreach (var current in match.Events.Skip(1))
        {
            current.Seq.ShouldBe(previous.Seq + 1);
            current.Period.ShouldBeGreaterThanOrEqualTo(previous.Period);
            if (current.Period == previous.Period)
            {
                current.ClockMs.ShouldBeGreaterThanOrEqualTo(previous.ClockMs);
            }

            current.WallTimeUtc.ShouldBeGreaterThanOrEqualTo(previous.WallTimeUtc);
            previous = current;
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Locations_stay_on_the_pitch(long seed)
    {
        var match = MatchSimulator.Simulate(seed);
        foreach (var e in match.Events)
        {
            e.Location.X.ShouldBeInRange(0, Pitch.LengthM);
            e.Location.Y.ShouldBeInRange(0, Pitch.WidthM);
            if (e.EndLocation is { } end)
            {
                end.X.ShouldBeInRange(0, Pitch.LengthM);
                end.Y.ShouldBeInRange(0, Pitch.WidthM);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Every_player_in_an_event_is_on_a_team_sheet(long seed)
    {
        var match = MatchSimulator.Simulate(seed);
        var known = match.Document.Home.Starters.Concat(match.Document.Home.Bench)
            .Concat(match.Document.Away.Starters).Concat(match.Document.Away.Bench)
            .Select(p => p.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var e in match.Events)
        {
            foreach (var id in new[] { e.PlayerId, e.ReceiverId, e.AssistId, e.FouledPlayerId, e.PlayerOffId, e.PlayerOnId })
            {
                if (id is not null)
                {
                    known.ShouldContain(id);
                }
            }
        }
    }

    [Fact]
    public void Nothing_in_the_league_names_a_real_club_or_competition()
    {
        var forbidden = new[] { "Withheld01", "Withheld02", "Withheld03", "Withheld04", "Withheld05", "Withheld06", "Withheld07", "Withheld08", "Withheld09", "Withheld10" };
        foreach (var club in MeridianLeague.Clubs)
        {
            foreach (var word in forbidden)
            {
                club.Club.Name.ShouldNotContain(word);
            }
        }

        foreach (var word in forbidden)
        {
            MeridianLeague.Name.ShouldNotContain(word);
        }
    }
}
