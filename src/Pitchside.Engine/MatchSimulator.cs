using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>
/// Possession-level simulator. A match is a sequence of possessions; each possession is a short chain of passes
/// and carries that ends in a shot, a foul, the ball going out, or a turnover. Rates come from
/// <see cref="SimulationOptions"/> and are modulated by club ratings, game state and period.
/// </summary>
public sealed class MatchSimulator
{
    private const int HalfLengthMs = 45 * 60 * 1000;

    private readonly Pcg32 _rng;
    private readonly SimulationOptions _options;
    private readonly DateTimeOffset _kickoff;
    private readonly List<MatchEvent> _events = [];
    private readonly SideState _home;
    private readonly SideState _away;
    private readonly MatchDocument _document;
    private readonly TrackingModel _tracking;

    private int _seq;
    private int _period;
    private int _clockMs;
    private int _possessionId;
    private int _wallOffsetMs;
    private int _nextTrackingMs;
    private readonly double _tempo;
    private readonly double _homeForm;
    private readonly double _awayForm;

    private MatchSimulator(long seed, EngineClub home, EngineClub away, DateTimeOffset kickoff, SimulationOptions options)
    {
        _rng = new Pcg32(unchecked((ulong)seed));
        _options = options;
        _kickoff = kickoff;
        var homeSheet = SquadFactory.Create(home, _rng);
        var awaySheet = SquadFactory.Create(away, _rng);
        _document = new MatchDocument(
            SchemaVersion.Current,
            MatchIdFor(seed),
            seed,
            MeridianLeague.Name,
            $"{home.Club.Name} Stadium",
            kickoff,
            homeSheet,
            awaySheet);
        _home = new SideState(home, homeSheet, true, _rng);
        _away = new SideState(away, awaySheet, false, _rng);
        _tracking = new TrackingModel(seed);
        // Matches differ in tempo: a log-normal multiplier with mean 1 on both sides' shot rates gives the
        // real spread of goalless and high-scoring games that fixed rates cannot produce.
        _tempo = Math.Exp(_rng.Gaussian(-0.5 * options.TempoSpread * options.TempoSpread, options.TempoSpread));
        // Each side also brings its own form on the day, independent of the other's, which is what makes
        // one-sided matches as common as they really are.
        _homeForm = Math.Exp(_rng.Gaussian(-0.5 * options.FormSpread * options.FormSpread, options.FormSpread));
        _awayForm = Math.Exp(_rng.Gaussian(-0.5 * options.FormSpread * options.FormSpread, options.FormSpread));
    }

    /// <summary>The id of the match a seed produces, known without simulating it.</summary>
    public static string MatchIdFor(long seed) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"MER-{seed}");

    public static SimulatedMatch Simulate(long seed, EngineClub home, EngineClub away, DateTimeOffset kickoff, SimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(away);
        var simulator = new MatchSimulator(seed, home, away, kickoff, options ?? SimulationOptions.Default);
        return simulator.Run();
    }

    /// <summary>Picks two distinct clubs from the league by seed and simulates them.</summary>
    public static SimulatedMatch Simulate(long seed, SimulationOptions? options = null)
    {
        var picker = new Pcg32(unchecked((ulong)seed), 99UL);
        var homeIndex = picker.Next(MeridianLeague.ClubCount);
        var awayIndex = (homeIndex + 1 + picker.Next(MeridianLeague.ClubCount - 1)) % MeridianLeague.ClubCount;
        var kickoff = new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero).AddDays(picker.Next(60));
        return Simulate(seed, MeridianLeague.Club(homeIndex), MeridianLeague.Club(awayIndex), kickoff, options);
    }

    private SimulatedMatch Run()
    {
        PlayPeriod(1, _home, (int)(_rng.Range(1.0, 3.0) * 60_000));
        // The second half's wall clock starts after the first half's stoppage time and a fifteen-minute break.
        _wallOffsetMs = _clockMs + (15 * 60_000);
        PlayPeriod(2, _away, (int)(_rng.Range(3.0, 6.5) * 60_000));
        Emit(EventType.FullTime, _home, null, Centre(), homeScore: _home.Goals, awayScore: _away.Goals);
        return new SimulatedMatch(_document, _events);
    }

    private void PlayPeriod(int period, SideState kicksOff, int stoppageMs)
    {
        _period = period;
        _clockMs = 0;
        var endMs = HalfLengthMs + stoppageMs;
        var inPossession = kicksOff;
        var start = new PossessionStart(Centre(), PossessionKind.KickOff);
        _nextTrackingMs = 60_000;
        _tracking.StartInterval(_home.OnPitch.Concat(_away.OnPitch), 0);
        Emit(EventType.KickOff, inPossession, OutfieldPlayer(inPossession, Position.Midfielder), Centre());
        _clockMs += 1000;

        while (_clockMs < endMs)
        {
            // Tracking ticks fall at the first possession boundary after each minute rolls over, home side first,
            // at the same point of the loop on every run so the engine stays deterministic.
            if (_clockMs >= _nextTrackingMs)
            {
                EmitTracking(_home);
                EmitTracking(_away);
                _nextTrackingMs = ((_clockMs / 60_000) + 1) * 60_000;
            }

            ApplySubstitutions(_home);
            ApplySubstitutions(_away);
            var result = PlayPossession(inPossession, Opponent(inPossession), start);
            inPossession = result.NextInPossession;
            start = result.NextStart;
        }

        Emit(EventType.HalfEnd, inPossession, null, Centre());
    }

    private PossessionResult PlayPossession(SideState team, SideState opponent, PossessionStart start)
    {
        _possessionId++;
        if (start.Kind == PossessionKind.Penalty)
        {
            return ResolvePenalty(team, opponent);
        }

        var location = start.Location;
        var passes = _rng.GeometricAtLeastOne(_options.MeanPasses);
        var shotRate = start.Kind == PossessionKind.Corner ? _options.CornerShotRate : ShotRate(team, opponent);
        var endsInShot = _rng.Chance(shotRate);
        var carrier = OutfieldPlayer(team, PreferredPosition(location));
        string? lastPasser = null;

        if (start.Kind == PossessionKind.Corner)
        {
            Emit(EventType.Corner, team, carrier, location);
            _clockMs += 15_000;
            passes = 1;
        }

        for (var i = 0; i < passes; i++)
        {
            if (_clockMs > HalfLengthMs + 7 * 60_000)
            {
                break;
            }

            var isLast = i == passes - 1;
            if (!endsInShot && isLast)
            {
                break;
            }

            MaybePressure(opponent, location);

            if (_rng.Chance(0.18))
            {
                var carryEnd = PassModel.SampleEnd(_rng, location, forward: true);
                var carryLength = location.DistanceTo(carryEnd);
                var durationS = Math.Max(1.0, carryLength / _rng.Range(3.5, 8.5));
                var carrySpeed = Math.Round(carryLength / durationS * 3.6, 1);
                Emit(EventType.Carry, team, carrier, location,
                    endLocation: carryEnd, lengthM: Math.Round(carryLength, 1), speedKph: carrySpeed);
                _tracking.Carry(carrier, carryLength, carrySpeed);
                _clockMs += (int)(durationS * 1000);
                location = carryEnd;
                continue;
            }

            var end = PassModel.SampleEnd(_rng, location, forward: _rng.Chance(0.72));
            var receiver = OutfieldPlayer(team, PreferredPosition(end), exclude: carrier);
            var difficulty = PassModel.Difficulty(location, end);
            if (_rng.Chance(difficulty * _options.PassFailureScale))
            {
                // An incomplete pass is cut out where it was aimed and the opponent restarts from there.
                Emit(EventType.Pass, team, carrier, location,
                    endLocation: end, lengthM: Math.Round(location.DistanceTo(end), 1), difficulty: difficulty, completed: false);
                _clockMs += (int)_rng.Range(1500, 4000);
                Emit(EventType.Turnover, team, carrier, end, reason: TurnoverReason.Interception);
                _clockMs += 2000;
                return new PossessionResult(opponent, new PossessionStart(Mirror(end), PossessionKind.OpenPlay));
            }

            Emit(EventType.Pass, team, carrier, location,
                endLocation: end, lengthM: Math.Round(location.DistanceTo(end), 1), difficulty: difficulty, completed: true, receiverId: receiver);
            _clockMs += (int)_rng.Range(2500, 6500);
            lastPasser = carrier;
            carrier = receiver;
            location = end;
        }

        if (endsInShot)
        {
            return ResolveShot(team, opponent, carrier, lastPasser);
        }

        var foulRate = location.X > 88 ? _options.FoulRate * _options.BoxFoulFactor : _options.FoulRate;
        if (_rng.Chance(foulRate))
        {
            return ResolveFoul(team, opponent, carrier, location);
        }

        if (_rng.Chance(_options.OutOfPlayRate))
        {
            return ResolveOutOfPlay(team, opponent, carrier, location);
        }

        var reason = _rng.NextDouble() switch
        {
            < 0.45 => TurnoverReason.Interception,
            < 0.75 => TurnoverReason.Tackle,
            < 0.90 => TurnoverReason.BadTouch,
            _ => TurnoverReason.Clearance,
        };
        Emit(EventType.Turnover, team, carrier, location, reason: reason);
        _clockMs += 2000;
        return new PossessionResult(opponent, new PossessionStart(Mirror(location), PossessionKind.OpenPlay));
    }

    private PossessionResult ResolveShot(SideState team, SideState opponent, string shooter, string? lastPasser)
    {
        var shooterId = _rng.NextDouble() switch
        {
            < 0.55 => OutfieldPlayer(team, Position.Forward),
            < 0.90 => OutfieldPlayer(team, Position.Midfielder),
            _ => OutfieldPlayer(team, Position.Defender),
        };
        if (lastPasser is not null && shooterId == lastPasser)
        {
            shooterId = shooter;
        }

        var xg = Math.Round(ShotModel.SampleXg(_rng, out var location), 3);
        var bodyPart = ShotModel.SampleBodyPart(_rng);
        return FinishShot(team, opponent, shooterId, lastPasser, location, xg, bodyPart, penalty: false);
    }

    /// <summary>A penalty is a possession of one event: a shot from the spot by the side's designated taker.</summary>
    private PossessionResult ResolvePenalty(SideState team, SideState opponent)
    {
        var outfield = team.OnPitch.Where(p => p.Position != Position.Goalkeeper).ToList();
        var forwards = outfield.Where(p => p.Position == Position.Forward).ToList();
        var pool = forwards.Count > 0 ? forwards : outfield.Count > 0 ? outfield : [.. team.OnPitch];
        var taker = pool.MaxBy(p => p.ShirtNumber)!.Id;
        var bodyPart = _rng.Chance(0.75) ? BodyPart.RightFoot : BodyPart.LeftFoot;
        _clockMs += 30_000;
        return FinishShot(team, opponent, taker, null, ShotModel.PenaltySpot, ShotModel.PenaltyXg, bodyPart, penalty: true);
    }

    private PossessionResult FinishShot(
        SideState team, SideState opponent, string shooterId, string? lastPasser, Location location, double xg, BodyPart bodyPart, bool penalty)
    {
        var outcome = ShotModel.ResolveOutcome(_rng, xg);
        var onTarget = outcome is ShotOutcome.Goal or ShotOutcome.Saved;
        _clockMs += 1500;
        Emit(EventType.Shot, team, shooterId, location,
            xg: xg, onTarget: onTarget, outcome: outcome, shotSpeedKph: ShotModel.SampleShotSpeedKph(_rng, bodyPart),
            bodyPart: bodyPart, assistId: outcome == ShotOutcome.Goal ? lastPasser : null, penalty: penalty ? true : null);
        team.Shots++;
        if (onTarget)
        {
            team.ShotsOnTarget++;
        }

        switch (outcome)
        {
            case ShotOutcome.Goal:
                team.Goals++;
                Emit(EventType.Goal, team, shooterId, location, assistId: lastPasser, homeScore: _home.Goals, awayScore: _away.Goals);
                _clockMs += 60_000;
                return new PossessionResult(opponent, new PossessionStart(Centre(), PossessionKind.KickOff));
            case ShotOutcome.Saved:
                _clockMs += 3000;
                if (_rng.Chance(0.12))
                {
                    return CornerFor(team);
                }

                return new PossessionResult(opponent, new PossessionStart(new Location(8, 34), PossessionKind.OpenPlay));
            case ShotOutcome.Blocked:
                _clockMs += 2000;
                if (_rng.Chance(0.22))
                {
                    return CornerFor(team);
                }

                return _rng.Chance(0.5)
                    ? new PossessionResult(team, new PossessionStart(new Location(78, _rng.Range(10, 58)), PossessionKind.OpenPlay))
                    : new PossessionResult(opponent, new PossessionStart(Mirror(location), PossessionKind.OpenPlay));
            default:
                _clockMs += 20_000;
                Emit(EventType.OutOfPlay, team, shooterId, new Location(104.5, location.Y), restart: Restart.GoalKick);
                return new PossessionResult(opponent, new PossessionStart(new Location(6, 34), PossessionKind.OpenPlay));
        }
    }

    private PossessionResult CornerFor(SideState team)
    {
        team.Corners++;
        var flagY = _rng.Chance(0.5) ? 0.5 : Pitch.WidthM - 0.5;
        return new PossessionResult(team, new PossessionStart(new Location(Pitch.LengthM - 0.5, flagY), PossessionKind.Corner));
    }

    private PossessionResult ResolveFoul(SideState team, SideState opponent, string fouled, Location location)
    {
        var fouler = OutfieldPlayer(opponent, PreferredPosition(Mirror(location)));
        // Referees are slower to book a player already on a yellow, which is why second yellows are rare.
        // Away sides are booked slightly more often per foul in the real data (1.84 against 1.64 on near-equal fouls).
        var yellowChance = _options.YellowPerFoul * (opponent.IsHome ? 0.96 : 1.05);
        if (opponent.Booked.Contains(fouler))
        {
            yellowChance *= 0.15;
        }

        var yellow = _rng.Chance(yellowChance);
        var straightRed = !yellow && _rng.Chance(_options.RedPerFoul);
        opponent.Fouls++;
        Emit(EventType.Foul, opponent, fouler, Mirror(location), fouledPlayerId: fouled, cardAwarded: yellow || straightRed);
        _clockMs += 25_000;

        if (yellow)
        {
            var second = !opponent.Booked.Add(fouler);
            opponent.Yellows++;
            Emit(EventType.Card, opponent, fouler, Mirror(location), colour: second ? CardColour.Red : CardColour.Yellow, secondYellow: second);
            if (second)
            {
                opponent.Reds++;
                opponent.SendOff(fouler);
            }
        }
        else if (straightRed)
        {
            opponent.Reds++;
            opponent.SendOff(fouler);
            Emit(EventType.Card, opponent, fouler, Mirror(location), colour: CardColour.Red, secondYellow: false);
        }

        if (location.X > 88 && _rng.Chance(_options.PenaltyPerBoxFoul))
        {
            return new PossessionResult(team, new PossessionStart(ShotModel.PenaltySpot, PossessionKind.Penalty));
        }

        return new PossessionResult(team, new PossessionStart(location, PossessionKind.OpenPlay));
    }

    private void EmitTracking(SideState side)
    {
        var entries = _tracking.Tick(side.OnPitch, _clockMs);
        Emit(EventType.Tracking, side, null, Centre(), tracking: entries);
    }

    private PossessionResult ResolveOutOfPlay(SideState team, SideState opponent, string carrier, Location location)
    {
        _clockMs += 12_000;
        // Sides that attack more win more corners: scale the corner chance by attacking pressure.
        var cornerChance = 0.29 * ShotRate(team, opponent) / 0.085;
        if (location.X > 70 && _rng.Chance(cornerChance))
        {
            Emit(EventType.OutOfPlay, opponent, null, new Location(104.5, location.Y), restart: Restart.Corner);
            return CornerFor(team);
        }

        if (location.X > 88 && _rng.Chance(0.5))
        {
            Emit(EventType.OutOfPlay, team, carrier, new Location(104.5, location.Y), restart: Restart.GoalKick);
            return new PossessionResult(opponent, new PossessionStart(new Location(6, 34), PossessionKind.OpenPlay));
        }

        var edgeY = location.Y < Pitch.WidthM / 2 ? 0.5 : Pitch.WidthM - 0.5;
        Emit(EventType.OutOfPlay, team, carrier, new Location(location.X, edgeY), restart: Restart.ThrowIn);
        return _rng.Chance(0.5)
            ? new PossessionResult(team, new PossessionStart(new Location(location.X, edgeY), PossessionKind.OpenPlay))
            : new PossessionResult(opponent, new PossessionStart(Mirror(new Location(location.X, edgeY)), PossessionKind.OpenPlay));
    }

    private void MaybePressure(SideState opponent, Location ballLocation)
    {
        if (ballLocation.X > 35 && _rng.Chance(0.28))
        {
            var presser = OutfieldPlayer(opponent, PreferredPosition(Mirror(ballLocation)));
            Emit(EventType.Pressure, opponent, presser, Mirror(ballLocation), intensity: Math.Round(_rng.Range(0.3, 1.0), 2));
        }
    }

    private void ApplySubstitutions(SideState side)
    {
        var matchMinute = ((_period - 1) * 45) + (_clockMs / 60_000);
        while (side.NextSubstitutionMinute(out var minute) && matchMinute >= minute)
        {
            var off = side.PickPlayerToReplace(_rng);
            var on = side.TakeFromBench(off);
            if (off is null || on is null)
            {
                side.SkipSubstitution();
                continue;
            }

            side.CompleteSubstitution(off, on);
            _tracking.PlayerOn(on, _clockMs);
            Emit(EventType.Substitution, side, on, new Location(52.5, 0.5), playerOffId: off, playerOnId: on);
        }
    }

    private double ShotRate(SideState team, SideState opponent)
    {
        var baseRate = team.IsHome ? _options.HomeShotRate : _options.AwayShotRate;
        var form = team.IsHome ? _homeForm : _awayForm;
        var rate = baseRate * _tempo * form * team.Club.Attack / opponent.Club.Defence;
        var diff = team.Goals - opponent.Goals;
        if (diff > 0)
        {
            rate *= _options.LeadingFactor;
        }
        else if (diff < 0)
        {
            rate *= _options.TrailingFactor;
        }

        rate *= _period == 1 ? _options.FirstHalfFactor : 2 - _options.FirstHalfFactor;
        // Ten men shoot less; eleven against ten shoot more.
        rate *= Math.Pow(0.85, team.PlayersSentOff) * Math.Pow(1.15, opponent.PlayersSentOff);
        return Math.Clamp(rate, 0.01, 0.6);
    }

    private SideState Opponent(SideState side) => ReferenceEquals(side, _home) ? _away : _home;

    private static Location Centre() => new(Pitch.LengthM / 2, Pitch.WidthM / 2);

    private static Location Mirror(Location location) => new(
        Math.Round(Pitch.LengthM - location.X, 1), Math.Round(Pitch.WidthM - location.Y, 1));

    private static Position PreferredPosition(Location location) => location.X switch
    {
        < 35 => Position.Defender,
        < 70 => Position.Midfielder,
        _ => Position.Forward,
    };

    private string OutfieldPlayer(SideState side, Position preferred, string? exclude = null)
    {
        var candidates = side.OnPitch.Where(p => p.Position != Position.Goalkeeper && p.Id != exclude).ToList();
        if (candidates.Count == 0)
        {
            return side.OnPitch[0].Id;
        }

        var preferredPool = candidates.Where(p => p.Position == preferred).ToList();
        return _rng.Chance(0.65) && preferredPool.Count > 0 ? _rng.Pick(preferredPool).Id : _rng.Pick(candidates).Id;
    }

    private void Emit(
        EventType type,
        SideState side,
        string? playerId,
        Location location,
        Location? endLocation = null,
        double? lengthM = null,
        double? difficulty = null,
        bool? completed = null,
        string? receiverId = null,
        double? speedKph = null,
        double? xg = null,
        bool? onTarget = null,
        ShotOutcome? outcome = null,
        double? shotSpeedKph = null,
        BodyPart? bodyPart = null,
        string? assistId = null,
        bool? penalty = null,
        int? homeScore = null,
        int? awayScore = null,
        TurnoverReason? reason = null,
        Restart? restart = null,
        string? fouledPlayerId = null,
        bool? cardAwarded = null,
        CardColour? colour = null,
        bool? secondYellow = null,
        string? playerOffId = null,
        string? playerOnId = null,
        double? intensity = null,
        IReadOnlyList<TrackingEntry>? tracking = null)
    {
        _seq++;
        var elapsed = TimeSpan.FromMilliseconds(_wallOffsetMs + _clockMs);
        _events.Add(new MatchEvent
        {
            Id = $"{_document.MatchId}-{_seq:00000}",
            MatchId = _document.MatchId,
            Seq = _seq,
            Period = _period,
            ClockMs = _clockMs,
            WallTimeUtc = _kickoff + elapsed,
            Type = type,
            TeamId = side.Club.Club.Id,
            PlayerId = playerId,
            Location = location,
            PossessionId = _possessionId,
            EndLocation = endLocation,
            LengthM = lengthM,
            Difficulty = difficulty,
            Completed = completed,
            ReceiverId = receiverId,
            SpeedKph = speedKph,
            Xg = xg,
            OnTarget = onTarget,
            Outcome = outcome,
            ShotSpeedKph = shotSpeedKph,
            BodyPart = bodyPart,
            AssistId = assistId,
            Penalty = penalty,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Reason = reason,
            Restart = restart,
            FouledPlayerId = fouledPlayerId,
            CardAwarded = cardAwarded,
            Colour = colour,
            SecondYellow = secondYellow,
            PlayerOffId = playerOffId,
            PlayerOnId = playerOnId,
            Intensity = intensity,
            Tracking = tracking,
        });
    }
}
