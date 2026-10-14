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

    [Theory]
    [MemberData(nameof(Seeds))]
    public void An_incomplete_pass_is_always_followed_by_a_turnover_by_the_same_side(long seed)
    {
        var events = MatchSimulator.Simulate(seed).Events;
        var incomplete = 0;
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Type != EventType.Pass || events[i].Completed != false)
            {
                continue;
            }

            incomplete++;
            var next = events[i + 1];
            next.Type.ShouldBe(EventType.Turnover);
            next.TeamId.ShouldBe(events[i].TeamId);
            next.Reason.ShouldBe(TurnoverReason.Interception);
            next.PossessionId.ShouldBe(events[i].PossessionId);
            events[i].ReceiverId.ShouldBeNull();
        }

        incomplete.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void A_penalty_shot_has_the_penalty_flag_spot_and_xg()
    {
        var penalties = Enumerable.Range(1, 60)
            .SelectMany(seed => MatchSimulator.Simulate(seed).Events)
            .Where(e => e.Penalty == true)
            .ToList();

        penalties.ShouldNotBeEmpty();
        foreach (var shot in penalties)
        {
            shot.Type.ShouldBe(EventType.Shot);
            shot.Xg.ShouldBe(0.76);
            shot.Location.ShouldBe(new Location(94, 34));
            shot.BodyPart.ShouldBeOneOf(BodyPart.RightFoot, BodyPart.LeftFoot);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void A_penalty_is_a_possession_of_one_shot_after_a_foul_in_the_final_third(long seed)
    {
        var events = MatchSimulator.Simulate(seed + 100).Events;
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Penalty != true)
            {
                continue;
            }

            events.Count(e => e.PossessionId == events[i].PossessionId && e.Type is EventType.Shot or EventType.Pass or EventType.Carry or EventType.Turnover).ShouldBe(1);
            var foul = events.Take(i).Last(e => e.Type == EventType.Foul);
            foul.TeamId.ShouldNotBe(events[i].TeamId);
            foul.Location.X.ShouldBeLessThan(105 - 88);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Tracking_ticks_are_a_minute_apart_per_side_and_list_exactly_the_players_on_the_pitch(long seed)
    {
        var match = MatchSimulator.Simulate(seed);
        var onPitch = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [match.Document.Home.Club.Id] = match.Document.Home.Starters.Select(p => p.Id).ToHashSet(StringComparer.Ordinal),
            [match.Document.Away.Club.Id] = match.Document.Away.Starters.Select(p => p.Id).ToHashSet(StringComparer.Ordinal),
        };
        var lastTick = new Dictionary<(string Team, int Period), int>();
        var ticks = 0;

        foreach (var e in match.Events)
        {
            switch (e.Type)
            {
                case EventType.Substitution:
                    onPitch[e.TeamId].Remove(e.PlayerOffId!);
                    onPitch[e.TeamId].Add(e.PlayerOnId!);
                    break;
                case EventType.Card when e.Colour == CardColour.Red:
                    onPitch[e.TeamId].Remove(e.PlayerId!);
                    break;
                case EventType.Tracking:
                    ticks++;
                    e.PlayerId.ShouldBeNull();
                    e.Location.ShouldBe(new Location(Pitch.LengthM / 2, Pitch.WidthM / 2));
                    e.Tracking.ShouldNotBeNull();
                    e.Tracking.Select(t => t.PlayerId).ToHashSet(StringComparer.Ordinal).SetEquals(onPitch[e.TeamId]).ShouldBeTrue();
                    e.Tracking.Count.ShouldBe(onPitch[e.TeamId].Count);
                    foreach (var entry in e.Tracking)
                    {
                        entry.DistanceM.ShouldBeGreaterThan(0);
                        entry.TopSpeedKph.ShouldBeInRange(12, 40);
                    }

                    var minute = e.ClockMs / 60_000;
                    minute.ShouldBeGreaterThanOrEqualTo(1);
                    if (lastTick.TryGetValue((e.TeamId, e.Period), out var previous))
                    {
                        minute.ShouldBeGreaterThan(previous);
                    }

                    lastTick[(e.TeamId, e.Period)] = minute;
                    break;
                default:
                    break;
            }
        }

        ticks.ShouldBeGreaterThan(150);
    }

    [Fact]
    public void Work_rates_are_within_the_contract_range()
    {
        var match = MatchSimulator.Simulate(7);
        foreach (var player in match.Document.Home.Starters.Concat(match.Document.Home.Bench).Concat(match.Document.Away.Starters).Concat(match.Document.Away.Bench))
        {
            player.WorkRate.ShouldBeInRange(95, 125);
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
