using System.Collections.Immutable;
using Pitchside.Contracts;

namespace Pitchside.Stats;

/// <summary>
/// The incremental stats engine. <see cref="Apply"/> touches only the accumulators an event affects, so a whole
/// match can be snapshotted at every event; <see cref="Snapshot"/> materialises and rounds.
/// </summary>
public sealed class MatchStatsEngine : IMatchStatsEngine
{
    public MatchStatsState Start(MatchDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var roster = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var sheet in new[] { document.Home, document.Away })
        {
            foreach (var player in sheet.Starters.Concat(sheet.Bench))
            {
                roster[player.Id] = sheet.Club.Id;
            }
        }

        return new MatchStatsState
        {
            MatchId = document.MatchId,
            HomeId = document.Home.Club.Id,
            AwayId = document.Away.Club.Id,
            Roster = roster.ToImmutable(),
            HomeStarters = [.. document.Home.Starters.Select(p => p.Id)],
            AwayStarters = [.. document.Away.Starters.Select(p => p.Id)],
        };
    }

    public StepResult Apply(MatchStatsState state, MatchEvent matchEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(matchEvent);
        var e = matchEvent;
        var tags = new List<Tag>();
        var home = state.Home;
        var away = state.Away;

        // Possession time accrues to whoever had the ball between the previous event and this one.
        if (state.InPossession is { } owner && state.Period == e.Period)
        {
            var elapsed = Math.Max(0, e.ClockMs - state.ClockMs);
            if (owner == state.HomeId)
            {
                home = home with { PossessionMs = home.PossessionMs + elapsed };
            }
            else
            {
                away = away with { PossessionMs = away.PossessionMs + elapsed };
            }
        }

        var next = state with
        {
            AsOfSeq = e.Seq,
            Period = e.Period,
            ClockMs = e.ClockMs,
            Home = home,
            Away = away,
            InPossession = EventRules.PossessionOwner(e, state.HomeId, state.AwayId) ?? state.InPossession,
        };

        if (e.PossessionId > state.LastPossessionId)
        {
            next = next with { LastPossessionId = e.PossessionId, Possessions = state.Possessions + 1 };
        }

        next = e.Type switch
        {
            EventType.KickOff => KickOff(next, e),
            EventType.HalfEnd => HalfEnd(next, e),
            EventType.FullTime => next with { State = FactKeys.StateFullTime },
            EventType.Pass => Pass(next, e, tags),
            EventType.Carry => Carry(next, e, tags),
            EventType.Shot => Shot(next, e, tags),
            EventType.Goal => Goal(next, e),
            EventType.Corner => UpdateSide(next, e.TeamId, s => s with { Corners = s.Corners.Add(1, e.Id) }),
            EventType.Foul => UpdatePlayer(
                UpdateSide(next, e.TeamId, s => s with { Fouls = s.Fouls.Add(1, e.Id) }),
                e.PlayerId, p => p with { Fouls = p.Fouls.Add(1, e.Id) }),
            EventType.Card => Card(next, e),
            EventType.Substitution => Substitution(next, e),
            EventType.Pressure => UpdatePlayer(
                UpdateSide(next, e.TeamId, s => s with { Pressures = s.Pressures.Add(1, e.Id) }),
                e.PlayerId, p => p with { Pressures = p.Pressures.Add(1, e.Id) }),
            EventType.Tracking => Tracking(next, e, tags),
            _ => next,
        };

        if (e.Type != EventType.KickOff)
        {
            next = next with { Window = next.Window.Add(e.ClockMs, e) };
        }

        return new StepResult(next, tags);
    }

    public FactSheet Snapshot(MatchStatsState state) => FactSheetBuilder.Build(state);

    private static MatchStatsState KickOff(MatchStatsState state, MatchEvent e)
    {
        var players = state.Players;
        if (e.Period == 1)
        {
            foreach (var id in state.HomeStarters.Concat(state.AwayStarters))
            {
                players = players.SetItem(id, new PlayerAccumulator
                {
                    PlayerId = id,
                    TeamId = state.Roster[id],
                    OnPitch = true,
                    OnSinceMs = e.ClockMs,
                });
            }
        }
        else
        {
            foreach (var (id, player) in players)
            {
                if (player.OnPitch)
                {
                    players = players.SetItem(id, player with { OnSinceMs = e.ClockMs });
                }
            }
        }

        return state with
        {
            State = e.Period == 1 ? FactKeys.StateFirstHalf : FactKeys.StateSecondHalf,
            Players = players,
            Window = state.Window.Clear().Add(e.ClockMs, e),
        };
    }

    private static MatchStatsState HalfEnd(MatchStatsState state, MatchEvent e)
    {
        var players = state.Players;
        foreach (var (id, player) in players)
        {
            if (player.OnSinceMs is not null)
            {
                players = players.SetItem(id, player.StopClock(e.ClockMs));
            }
        }

        return state with
        {
            State = e.Period == 1 ? FactKeys.StateHalfTime : state.State,
            Players = players,
        };
    }

    private static MatchStatsState Pass(MatchStatsState state, MatchEvent e, List<Tag> tags)
    {
        var completed = e.Completed == true;
        var progressive = EventRules.IsProgressive(e);
        var entry = EventRules.IsFinalThirdEntry(e);
        var next = UpdateSide(state, e.TeamId, s => s with
        {
            Passes = s.Passes.Add(1, e.Id),
            DifficultySum = s.DifficultySum + (e.Difficulty ?? 0),
            PassesCompleted = completed ? s.PassesCompleted.Add(1, e.Id) : s.PassesCompleted,
            ProgressivePasses = progressive ? s.ProgressivePasses.Add(1, e.Id) : s.ProgressivePasses,
            FinalThirdEntries = entry ? s.FinalThirdEntries.Add(1, e.Id) : s.FinalThirdEntries,
        });
        next = UpdatePlayer(next, e.PlayerId, p => p with
        {
            Passes = p.Passes.Add(1, e.Id),
            PassesCompleted = completed ? p.PassesCompleted.Add(1, e.Id) : p.PassesCompleted,
            ProgressivePasses = progressive ? p.ProgressivePasses.Add(1, e.Id) : p.ProgressivePasses,
        });

        if (completed && e.Difficulty is { } difficulty && difficulty >= TagNames.HardPassDifficulty && e.PlayerId is { } passer)
        {
            next = RaiseTag(next, tags, new Tag(TagNames.PassHard, passer, e.Id, difficulty));
        }

        return next;
    }

    private static MatchStatsState Carry(MatchStatsState state, MatchEvent e, List<Tag> tags)
    {
        var next = state;
        if (EventRules.IsFinalThirdEntry(e))
        {
            next = UpdateSide(next, e.TeamId, s => s with { FinalThirdEntries = s.FinalThirdEntries.Add(1, e.Id) });
        }

        if (e.PlayerId is not { } carrier || !next.Players.TryGetValue(carrier, out var before))
        {
            return next;
        }

        var speed = e.SpeedKph ?? 0;
        var after = before with
        {
            Carries = before.Carries.Add(1, e.Id),
            TopSpeedKph = before.TopSpeedKph.Max(speed, e.Id),
            Sprints = speed >= TagNames.SprintCarryKph ? before.Sprints.Add(1, e.Id) : before.Sprints,
        };
        next = next with { Players = next.Players.SetItem(carrier, after) };
        return SpeedTags(next, tags, carrier, before.TopSpeedKph.Value, after.TopSpeedKph.Value, e.Id);
    }

    private static MatchStatsState Shot(MatchStatsState state, MatchEvent e, List<Tag> tags)
    {
        var xg = e.Xg ?? 0;
        var onTarget = e.OnTarget == true;
        var next = UpdateSide(state, e.TeamId, s => s with
        {
            Shots = s.Shots.Add(1, e.Id),
            Xg = s.Xg.Add(xg, e.Id),
            ShotsOnTarget = onTarget ? s.ShotsOnTarget.Add(1, e.Id) : s.ShotsOnTarget,
            BigChances = xg >= EventRules.BigChanceXg ? s.BigChances.Add(1, e.Id) : s.BigChances,
        });
        next = UpdatePlayer(next, e.PlayerId, p => p with { Shots = p.Shots.Add(1, e.Id), Xg = p.Xg.Add(xg, e.Id) });
        if (e.ShotSpeedKph is { } speed && speed >= TagNames.FastShotKph && e.PlayerId is { } shooter)
        {
            next = RaiseTag(next, tags, new Tag(TagNames.ShotFast, shooter, e.Id, speed));
        }

        return next;
    }

    private static MatchStatsState Goal(MatchStatsState state, MatchEvent e)
    {
        var next = UpdateSide(state, e.TeamId, s => s with { Goals = s.Goals.Add(1, e.Id) });
        next = UpdatePlayer(next, e.PlayerId, p => p with { Goals = p.Goals.Add(1, e.Id) });
        return UpdatePlayer(next, e.AssistId, p => p with { Assists = p.Assists.Add(1, e.Id) });
    }

    private static MatchStatsState Card(MatchStatsState state, MatchEvent e)
    {
        var red = e.Colour == CardColour.Red;
        var next = UpdateSide(state, e.TeamId, s => s with
        {
            Yellows = !red || e.SecondYellow == true ? s.Yellows.Add(1, e.Id) : s.Yellows,
            Reds = red ? s.Reds.Add(1, e.Id) : s.Reds,
        });
        return UpdatePlayer(next, e.PlayerId, p =>
        {
            var carded = p with { Cards = p.Cards.Add(1, e.Id) };
            return red ? carded.StopClock(e.ClockMs) with { OnPitch = false } : carded;
        });
    }

    private static MatchStatsState Substitution(MatchStatsState state, MatchEvent e)
    {
        var next = UpdatePlayer(state, e.PlayerOffId, p => p.StopClock(e.ClockMs) with { OnPitch = false });
        if (e.PlayerOnId is { } on && state.Roster.TryGetValue(on, out var team))
        {
            next = next with
            {
                Players = next.Players.SetItem(on, new PlayerAccumulator
                {
                    PlayerId = on,
                    TeamId = team,
                    OnPitch = true,
                    OnSinceMs = e.ClockMs,
                }),
            };
        }

        return next;
    }

    private static MatchStatsState Tracking(MatchStatsState state, MatchEvent e, List<Tag> tags)
    {
        if (e.Tracking is null)
        {
            return state;
        }

        var next = state;
        var sideDistance = 0.0;
        foreach (var entry in e.Tracking)
        {
            sideDistance += entry.DistanceM;
            if (!next.Players.TryGetValue(entry.PlayerId, out var before))
            {
                continue;
            }

            var after = before with
            {
                DistanceM = before.DistanceM.Add(entry.DistanceM, e.Id),
                TopSpeedKph = before.TopSpeedKph.Max(entry.TopSpeedKph, e.Id),
                Sprints = entry.TopSpeedKph >= TagNames.SprintTrackingKph ? before.Sprints.Add(1, e.Id) : before.Sprints,
            };
            next = next with { Players = next.Players.SetItem(entry.PlayerId, after) };
            next = SpeedTags(next, tags, entry.PlayerId, before.TopSpeedKph.Value, after.TopSpeedKph.Value, e.Id);
            next = DistanceTags(next, tags, entry.PlayerId, before.DistanceM.Value / 1000, after.DistanceM.Value / 1000, e.Id);
        }

        return UpdateSide(next, e.TeamId, s => s with { DistanceM = s.DistanceM.Add(sideDistance, e.Id) });
    }

    private static MatchStatsState SpeedTags(MatchStatsState state, List<Tag> tags, string playerId, double before, double after, string eventId)
    {
        var next = state;
        if (before < 30 && after >= 30)
        {
            next = RaiseTag(next, tags, new Tag(TagNames.Speed30, playerId, eventId, after));
        }

        if (before < 33 && after >= 33)
        {
            next = RaiseTag(next, tags, new Tag(TagNames.Speed33, playerId, eventId, after));
        }

        return next;
    }

    private static MatchStatsState DistanceTags(MatchStatsState state, List<Tag> tags, string playerId, double beforeKm, double afterKm, string eventId)
    {
        var next = state;
        if (beforeKm < 5 && afterKm >= 5)
        {
            next = RaiseTag(next, tags, new Tag(TagNames.Distance5, playerId, eventId, afterKm));
        }

        if (beforeKm < 10 && afterKm >= 10)
        {
            next = RaiseTag(next, tags, new Tag(TagNames.Distance10, playerId, eventId, afterKm));
        }

        return next;
    }

    private static MatchStatsState RaiseTag(MatchStatsState state, List<Tag> tags, Tag tag)
    {
        tags.Add(tag);
        return UpdatePlayer(state, tag.PlayerId, p => p with
        {
            TagEvents = !p.TagEvents.Ids.IsEmpty && p.TagEvents.Ids[^1] == tag.EventId ? p.TagEvents : p.TagEvents.Add(1, tag.EventId),
            TagNames = p.TagNames.Contains(tag.Name) ? p.TagNames : p.TagNames.Add(tag.Name),
        });
    }

    private static MatchStatsState UpdateSide(MatchStatsState state, string teamId, Func<SideAccumulator, SideAccumulator> update) =>
        teamId == state.HomeId ? state with { Home = update(state.Home) } : state with { Away = update(state.Away) };

    private static MatchStatsState UpdatePlayer(MatchStatsState state, string? playerId, Func<PlayerAccumulator, PlayerAccumulator> update) =>
        playerId is not null && state.Players.TryGetValue(playerId, out var player)
            ? state with { Players = state.Players.SetItem(playerId, update(player)) }
            : state;
}
