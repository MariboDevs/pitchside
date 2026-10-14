using Pitchside.Contracts;

namespace Pitchside.Stats.Tests;

/// <summary>
/// The correctness oracle: computes a full fact sheet from a prefix of events in one pass over the list, written
/// from the metric catalogue in WP-01 without reusing the engine's accumulators or rules.
/// </summary>
internal static class BatchStats
{
    private const int Cap = 50;

    public static FactSheet Compute(MatchDocument document, IReadOnlyList<MatchEvent> events)
    {
        var homeId = document.Home.Club.Id;
        var awayId = document.Away.Club.Id;
        var last = events.Count == 0 ? null : events[^1];
        var period = last?.Period ?? 0;
        var clock = last?.ClockMs ?? 0;
        var asOf = last?.Seq ?? 0;
        var facts = new List<Fact>();

        // Match state and clock.
        var state = "pre_match";
        foreach (var e in events)
        {
            state = e.Type switch
            {
                EventType.KickOff => e.Period == 1 ? "first_half" : "second_half",
                EventType.HalfEnd when e.Period == 1 => "half_time",
                EventType.FullTime => "full_time",
                _ => state,
            };
        }

        facts.Add(N("match.clock.minute", period == 0 ? 0 : ((period - 1) * 45) + (clock / 60_000) + 1, "min", 0, []));
        facts.Add(N("match.period", period, null, 0, []));
        facts.Add(new Fact { Key = "match.state", Value = FactValue.Of(state), Precision = 0, DerivedFrom = [] });
        var goals = events.Where(e => e.Type == EventType.Goal).ToList();
        facts.Add(N("match.score.home", goals.Count(g => g.TeamId == homeId), "goals", 0, Ids(goals.Where(g => g.TeamId == homeId))));
        facts.Add(N("match.score.away", goals.Count(g => g.TeamId == awayId), "goals", 0, Ids(goals.Where(g => g.TeamId == awayId))));
        var possessionStarts = events.Where(e => e.PossessionId > 0).GroupBy(e => e.PossessionId).Select(g => g.First()).ToList();
        facts.Add(N("match.possessions", possessionStarts.Count, "count", 0, Ids(possessionStarts)));

        // Rolling windows: the current period, the last ten (or five) minutes of period clock.
        var lastKickOff = events.Select((e, i) => (e, i)).LastOrDefault(x => x.e.Type == EventType.KickOff);
        var periodEvents = lastKickOff.e is null ? [] : events.Skip(lastKickOff.i).ToList();
        var ten = periodEvents.Where(e => e.ClockMs >= clock - 600_000).ToList();
        var five = periodEvents.Where(e => e.ClockMs >= clock - 300_000).ToList();
        var tenWindow = new FactWindow(ten.Count == 0 ? asOf : ten[0].Seq, asOf);
        var fiveWindow = new FactWindow(five.Count == 0 ? asOf : five[0].Seq, asOf);

        var rhythmPossessions = ten
            .Where(e => e.PossessionId > 0 && e.Type is not (EventType.KickOff or EventType.Tracking or EventType.HalfEnd or EventType.FullTime or EventType.Substitution))
            .Select(e => e.PossessionId).Distinct().Count();
        facts.Add(N("match.rhythm", rhythmPossessions / Math.Max(1.0, Math.Min(10.0, clock / 60_000.0)), "per min", 2, [], tenWindow));

        var open = ten.Count(e => e.Type is EventType.Pass or EventType.Carry or EventType.Shot or EventType.Turnover or EventType.Pressure or EventType.Foul or EventType.OutOfPlay);
        var controlled = ten.Count(e => (e.Type == EventType.Pass && e.Completed == true) || e.Type == EventType.Carry);
        facts.Add(N("match.control", open == 0 ? 0.5 : (double)controlled / open, "share", 2, [], tenWindow));

        double Momentum(string team) => five.Where(e => e.TeamId == team).Sum(e =>
            (e.Type == EventType.Shot ? 3 + (e.OnTarget == true ? 4 : 0) + (10 * (e.Xg ?? 0)) : 0)
            + (e.Type == EventType.Corner ? 2 : 0)
            + (EntersFinalThird(e) ? 1 : 0));
        var mh = Momentum(homeId);
        var ma = Momentum(awayId);
        facts.Add(N("match.momentum.home", mh + ma == 0 ? 0.5 : mh / (mh + ma), "share", 2, [], fiveWindow));
        facts.Add(N("match.momentum.away", mh + ma == 0 ? 0.5 : ma / (mh + ma), "share", 2, [], fiveWindow));

        // Possession time: the gap before each event goes to whoever the previous events showed in possession.
        var possessionMs = new Dictionary<string, double> { [homeId] = 0, [awayId] = 0 };
        string? owner = null;
        MatchEvent? previous = null;
        foreach (var e in events)
        {
            if (owner is not null && previous is not null && previous.Period == e.Period)
            {
                possessionMs[owner] += Math.Max(0, e.ClockMs - previous.ClockMs);
            }

            var other = e.TeamId == homeId ? awayId : homeId;
            // After a turnover or a goal the other side has the ball; after a goal kick or a corner the side that
            // put the ball out does not; a throw-in tells us nothing.
            owner = e.Type switch
            {
                EventType.KickOff or EventType.Pass or EventType.Carry or EventType.Shot or EventType.Corner => e.TeamId,
                EventType.Turnover or EventType.Goal or EventType.Pressure or EventType.Foul or EventType.Card => other,
                EventType.OutOfPlay when e.Restart is Restart.GoalKick or Restart.Corner => other,
                _ => owner,
            };
            previous = e;
        }

        // Players who have appeared, and their teams.
        var team = document.Home.Starters.Concat(document.Home.Bench).ToDictionary(p => p.Id, _ => homeId);
        foreach (var p in document.Away.Starters.Concat(document.Away.Bench))
        {
            team[p.Id] = awayId;
        }

        var appeared = new List<string>();
        if (events.Any(e => e.Type == EventType.KickOff))
        {
            appeared.AddRange(document.Home.Starters.Select(p => p.Id));
            appeared.AddRange(document.Away.Starters.Select(p => p.Id));
        }

        appeared.AddRange(events.Where(e => e.Type == EventType.Substitution).Select(e => e.PlayerOnId!));
        appeared = [.. appeared.Distinct().Order(StringComparer.Ordinal)];

        var topSpeed = appeared.ToDictionary(id => id, id => TopSpeed(events, id));

        foreach (var (scope, side, opponent) in new[] { ("home", homeId, awayId), ("away", awayId, homeId) })
        {
            var shots = events.Where(e => e.Type == EventType.Shot && e.TeamId == side).ToList();
            var passes = events.Where(e => e.Type == EventType.Pass && e.TeamId == side).ToList();
            var completed = passes.Where(e => e.Completed == true).ToList();
            var cards = events.Where(e => e.Type == EventType.Card && e.TeamId == side).ToList();
            var tracking = events.Where(e => e.Type == EventType.Tracking && e.TeamId == side).ToList();

            facts.Add(N($"{scope}.goals", goals.Count(g => g.TeamId == side), "goals", 0, Ids(goals.Where(g => g.TeamId == side))));
            facts.Add(N($"{scope}.xg", shots.Sum(s => s.Xg ?? 0), "xg", 2, Ids(shots)));
            facts.Add(N($"{scope}.shots", shots.Count, "count", 0, Ids(shots)));
            facts.Add(N($"{scope}.shotsOnTarget", shots.Count(s => s.OnTarget == true), "count", 0, Ids(shots.Where(s => s.OnTarget == true))));
            facts.Add(N($"{scope}.bigChances", shots.Count(s => s.Xg >= 0.30), "count", 0, Ids(shots.Where(s => s.Xg >= 0.30))));
            facts.Add(N($"{scope}.passes", passes.Count, "count", 0, Ids(passes)));
            facts.Add(N($"{scope}.passesCompleted", completed.Count, "count", 0, Ids(completed)));
            facts.Add(N($"{scope}.passCompletion", passes.Count == 0 ? 0 : (double)completed.Count / passes.Count, "share", 2, Ids(passes)));
            facts.Add(N($"{scope}.passDifficultyAvg", passes.Count == 0 ? 0 : passes.Sum(p => p.Difficulty ?? 0) / passes.Count, "difficulty", 2, Ids(passes)));
            var progressive = completed.Where(p => p.EndLocation!.Value.X - p.Location.X >= 10).ToList();
            facts.Add(N($"{scope}.progressivePasses", progressive.Count, "count", 0, Ids(progressive)));
            var entries = events.Where(e => e.TeamId == side && EntersFinalThird(e)).ToList();
            facts.Add(N($"{scope}.finalThirdEntries", entries.Count, "count", 0, Ids(entries)));
            var totalMs = possessionMs[homeId] + possessionMs[awayId];
            facts.Add(N($"{scope}.possessionShare", totalMs == 0 ? 0.5 : possessionMs[side] / totalMs, "share", 2, []));
            var corners = events.Where(e => e.Type == EventType.Corner && e.TeamId == side).ToList();
            facts.Add(N($"{scope}.corners", corners.Count, "count", 0, Ids(corners)));
            var fouls = events.Where(e => e.Type == EventType.Foul && e.TeamId == side).ToList();
            facts.Add(N($"{scope}.fouls", fouls.Count, "count", 0, Ids(fouls)));
            var yellows = cards.Where(c => c.Colour == CardColour.Yellow || c.SecondYellow == true).ToList();
            facts.Add(N($"{scope}.yellows", yellows.Count, "count", 0, Ids(yellows)));
            var reds = cards.Where(c => c.Colour == CardColour.Red).ToList();
            facts.Add(N($"{scope}.reds", reds.Count, "count", 0, Ids(reds)));
            var pressures = events.Where(e => e.Type == EventType.Pressure && e.TeamId == side).ToList();
            facts.Add(N($"{scope}.pressures", pressures.Count, "count", 0, Ids(pressures)));
            var windowPressures = ten.Count(e => e.Type == EventType.Pressure && e.TeamId == side);
            var windowOpponentPasses = ten.Count(e => e.Type == EventType.Pass && e.TeamId == opponent);
            facts.Add(N($"{scope}.pressingIntensity", windowOpponentPasses == 0 ? 0 : (double)windowPressures / windowOpponentPasses, "per pass", 2, [], tenWindow));
            facts.Add(N($"{scope}.distanceKm", tracking.Sum(t => t.Tracking!.Sum(x => x.DistanceM)) / 1000, "km", 2, Ids(tracking)));
            var fastest = appeared.Where(id => team[id] == side).Select(id => topSpeed[id]).Where(t => t.Value > 0)
                .Aggregate((Value: 0.0, Ids: (IReadOnlyList<string>)[]), (best, t) => t.Value > best.Value ? t : best);
            facts.Add(N($"{scope}.topSpeedKph", fastest.Value, "kph", 1, fastest.Ids));
        }

        foreach (var id in appeared)
        {
            facts.AddRange(PlayerFacts(events, id, clock, topSpeed[id]));
        }

        return new FactSheet(document.MatchId, asOf, period, clock, facts);
    }

    private static IEnumerable<Fact> PlayerFacts(IReadOnlyList<MatchEvent> events, string id, int clock, (double Value, IReadOnlyList<string> Ids) topSpeed)
    {
        var key = $"player.{id}.";
        var passes = events.Where(e => e.Type == EventType.Pass && e.PlayerId == id).ToList();
        var completed = passes.Where(e => e.Completed == true).ToList();
        var progressive = completed.Where(p => p.EndLocation!.Value.X - p.Location.X >= 10).ToList();
        var carries = events.Where(e => e.Type == EventType.Carry && e.PlayerId == id).ToList();
        var tracking = events.Where(e => e.Type == EventType.Tracking && e.Tracking!.Any(t => t.PlayerId == id)).ToList();
        var shots = events.Where(e => e.Type == EventType.Shot && e.PlayerId == id).ToList();
        var goals = events.Where(e => e.Type == EventType.Goal && e.PlayerId == id).ToList();
        var assists = events.Where(e => e.Type == EventType.Goal && e.AssistId == id).ToList();
        var pressures = events.Where(e => e.Type == EventType.Pressure && e.PlayerId == id).ToList();
        var fouls = events.Where(e => e.Type == EventType.Foul && e.PlayerId == id).ToList();
        var cards = events.Where(e => e.Type == EventType.Card && e.PlayerId == id).ToList();
        var sprints = events.Where(e =>
            (e.Type == EventType.Carry && e.PlayerId == id && e.SpeedKph >= 25)
            || (e.Type == EventType.Tracking && e.Tracking!.Any(t => t.PlayerId == id && t.TopSpeedKph >= 30))).ToList();
        var tags = Tags(events, id);

        yield return N(key + "passes", passes.Count, "count", 0, Ids(passes));
        yield return N(key + "passesCompleted", completed.Count, "count", 0, Ids(completed));
        yield return N(key + "passCompletion", passes.Count == 0 ? 0 : (double)completed.Count / passes.Count, "share", 2, Ids(passes));
        yield return N(key + "progressivePasses", progressive.Count, "count", 0, Ids(progressive));
        yield return N(key + "carries", carries.Count, "count", 0, Ids(carries));
        yield return N(key + "distanceKm", tracking.Sum(t => t.Tracking!.Single(x => x.PlayerId == id).DistanceM) / 1000, "km", 2, Ids(tracking));
        yield return N(key + "topSpeedKph", topSpeed.Value, "kph", 1, topSpeed.Ids);
        yield return N(key + "shots", shots.Count, "count", 0, Ids(shots));
        yield return N(key + "xg", shots.Sum(s => s.Xg ?? 0), "xg", 2, Ids(shots));
        yield return N(key + "goals", goals.Count, "goals", 0, Ids(goals));
        yield return N(key + "assists", assists.Count, "count", 0, Ids(assists));
        yield return N(key + "pressures", pressures.Count, "count", 0, Ids(pressures));
        yield return N(key + "fouls", fouls.Count, "count", 0, Ids(fouls));
        yield return N(key + "cards", cards.Count, "count", 0, Ids(cards));
        yield return N(key + "minutes", MinutesPlayed(events, id, clock), "min", 0, []);
        yield return N(key + "sprints", sprints.Count, "count", 0, Ids(sprints));
        yield return new Fact
        {
            Key = key + "tags",
            Value = FactValue.Of(string.Join(',', tags.Select(t => t.Name).Distinct())),
            Precision = 0,
            DerivedFrom = Ids(tags.Select(t => t.EventId).Distinct()),
        };
    }

    /// <summary>The fastest reading for a player, from carries and tracking, and the event that first set it.</summary>
    private static (double Value, IReadOnlyList<string> Ids) TopSpeed(IReadOnlyList<MatchEvent> events, string id)
    {
        var best = 0.0;
        IReadOnlyList<string> source = [];
        foreach (var e in events)
        {
            var reading = e.Type switch
            {
                EventType.Carry when e.PlayerId == id => e.SpeedKph ?? 0,
                EventType.Tracking => e.Tracking!.FirstOrDefault(t => t.PlayerId == id)?.TopSpeedKph ?? 0,
                _ => 0,
            };
            if (reading > best)
            {
                best = reading;
                source = [e.Id];
            }
        }

        return (best, source);
    }

    /// <summary>Every tag the player raised, recomputed by replaying thresholds over the events.</summary>
    internal static List<Tag> Tags(IReadOnlyList<MatchEvent> events, string id)
    {
        var tags = new List<Tag>();
        var top = 0.0;
        var distance = 0.0;
        foreach (var e in events)
        {
            if (e.Type == EventType.Pass && e.PlayerId == id && e.Completed == true && e.Difficulty >= 0.6)
            {
                tags.Add(new Tag("pass.hard", id, e.Id, e.Difficulty!.Value));
            }

            if (e.Type == EventType.Shot && e.PlayerId == id && e.ShotSpeedKph >= 100)
            {
                tags.Add(new Tag("shot.fast", id, e.Id, e.ShotSpeedKph!.Value));
            }

            double? reading = e.Type switch
            {
                EventType.Carry when e.PlayerId == id => e.SpeedKph ?? 0,
                EventType.Tracking => e.Tracking!.FirstOrDefault(t => t.PlayerId == id)?.TopSpeedKph,
                _ => null,
            };
            if (reading is { } speed)
            {
                var before = top;
                top = Math.Max(top, speed);
                if (before < 30 && top >= 30)
                {
                    tags.Add(new Tag("speed.30", id, e.Id, top));
                }

                if (before < 33 && top >= 33)
                {
                    tags.Add(new Tag("speed.33", id, e.Id, top));
                }
            }

            if (e.Type == EventType.Tracking && e.Tracking!.FirstOrDefault(t => t.PlayerId == id) is { } entry)
            {
                var before = distance / 1000;
                distance += entry.DistanceM;
                var after = distance / 1000;
                if (before < 5 && after >= 5)
                {
                    tags.Add(new Tag("distance.5", id, e.Id, after));
                }

                if (before < 10 && after >= 10)
                {
                    tags.Add(new Tag("distance.10", id, e.Id, after));
                }
            }
        }

        return tags;
    }

    /// <summary>Period-clock milliseconds on the pitch, from kick-off or coming on, to half-end, going off or now.</summary>
    private static int MinutesPlayed(IReadOnlyList<MatchEvent> events, string id, int clock)
    {
        var total = 0;
        int? since = null;
        var onPitch = false;
        foreach (var e in events)
        {
            switch (e.Type)
            {
                case EventType.KickOff when e.Period == 1:
                    onPitch = IsStarter(events, id);
                    since = onPitch ? e.ClockMs : null;
                    break;
                case EventType.KickOff when onPitch:
                    since = e.ClockMs;
                    break;
                case EventType.HalfEnd when since is not null:
                    total += e.ClockMs - since.Value;
                    since = null;
                    break;
                case EventType.Substitution when e.PlayerOnId == id:
                    onPitch = true;
                    since = e.ClockMs;
                    break;
                case EventType.Substitution when e.PlayerOffId == id:
                case EventType.Card when e.PlayerId == id && e.Colour == CardColour.Red:
                    if (since is not null)
                    {
                        total += e.ClockMs - since.Value;
                    }

                    since = null;
                    onPitch = false;
                    break;
                default:
                    break;
            }
        }

        if (since is not null)
        {
            total += clock - since.Value;
        }

        return total / 60_000;
    }

    private static bool IsStarter(IReadOnlyList<MatchEvent> events, string id) =>
        !events.Any(e => e.Type == EventType.Substitution && e.PlayerOnId == id);

    private static bool EntersFinalThird(MatchEvent e) =>
        ((e.Type == EventType.Pass && e.Completed == true) || e.Type == EventType.Carry)
        && e.EndLocation is { } end && e.Location.X <= 70 && end.X > 70;

    private static IReadOnlyList<string> Ids(IEnumerable<MatchEvent> events) => Ids(events.Select(e => e.Id));

    private static IReadOnlyList<string> Ids(IEnumerable<string> ids)
    {
        var list = ids.ToList();
        return list.Count > Cap ? list.GetRange(list.Count - Cap, Cap) : list;
    }

    private static Fact N(string key, double value, string? unit, int precision, IReadOnlyList<string> derivedFrom, FactWindow? window = null) => new()
    {
        Key = key,
        Value = FactValue.Of(Math.Round(value, precision, MidpointRounding.AwayFromZero)),
        Unit = unit,
        Precision = precision,
        DerivedFrom = derivedFrom,
        Window = window,
    };
}
