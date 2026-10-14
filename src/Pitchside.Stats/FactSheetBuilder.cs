using Pitchside.Contracts;

namespace Pitchside.Stats;

/// <summary>Turns accumulators into keyed, rounded facts. The only place values are rounded.</summary>
public static class FactSheetBuilder
{
    private const string UnitGoals = "goals";
    private const string UnitCount = "count";
    private const string UnitShare = "share";
    private const string UnitXg = "xg";
    private const string UnitKm = "km";
    private const string UnitKph = "kph";
    private const string UnitMinutes = "min";
    private const string UnitPerMinute = "per min";
    private const string UnitPerPass = "per pass";
    private const string UnitDifficulty = "difficulty";

    public static FactSheet Build(MatchStatsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var facts = new List<Fact>(64 + (state.Players.Count * 18));
        AddMatchFacts(state, facts);
        AddSideFacts(state, FactKeys.HomeScope, state.HomeId, state.Home, state.AwayId, facts);
        AddSideFacts(state, FactKeys.AwayScope, state.AwayId, state.Away, state.HomeId, facts);
        foreach (var player in state.Players.Values)
        {
            AddPlayerFacts(state, player, facts);
        }

        return new FactSheet(state.MatchId, state.AsOfSeq, state.Period, state.ClockMs, facts);
    }

    private static void AddMatchFacts(MatchStatsState state, List<Fact> facts)
    {
        facts.Add(Number(FactKeys.MatchClockMinute, EventRules.MatchMinute(state.Period, state.ClockMs), UnitMinutes, 0, []));
        facts.Add(Number(FactKeys.MatchPeriod, state.Period, null, 0, []));
        facts.Add(new Fact { Key = FactKeys.MatchState, Value = FactValue.Of(state.State), Precision = 0, DerivedFrom = [] });
        facts.Add(Number(FactKeys.MatchScoreHome, state.Home.Goals.Value, UnitGoals, 0, state.Home.Goals.Ids));
        facts.Add(Number(FactKeys.MatchScoreAway, state.Away.Goals.Value, UnitGoals, 0, state.Away.Goals.Ids));
        facts.Add(Number(FactKeys.MatchPossessions, state.Possessions, UnitCount, 0, []));

        var now = state.ClockMs;
        var tenMinutes = state.Window.Items.ToList();
        var fiveMinutes = state.Window.Since(now - EventRules.FiveMinutesMs).ToList();
        var tenWindow = WindowOf(tenMinutes, state.AsOfSeq);
        var fiveWindow = WindowOf(fiveMinutes, state.AsOfSeq);

        var possessions = tenMinutes.Where(EventRules.CountsForRhythm).Select(e => e.PossessionId).Distinct().Count();
        var minutes = Math.Max(1.0, Math.Min(10.0, now / 60_000.0));
        facts.Add(Number(FactKeys.MatchRhythm, possessions / minutes, UnitPerMinute, 2, [], tenWindow));

        var openPlay = tenMinutes.Count(EventRules.IsOpenPlay);
        var controlled = tenMinutes.Count(EventRules.IsControlled);
        facts.Add(Number(FactKeys.MatchControl, openPlay == 0 ? 0.5 : (double)controlled / openPlay, UnitShare, 2, [], tenWindow));

        var homeWeight = fiveMinutes.Where(e => e.TeamId == state.HomeId).Sum(EventRules.MomentumWeight);
        var awayWeight = fiveMinutes.Where(e => e.TeamId == state.AwayId).Sum(EventRules.MomentumWeight);
        var total = homeWeight + awayWeight;
        facts.Add(Number(FactKeys.MatchMomentumHome, total == 0 ? 0.5 : homeWeight / total, UnitShare, 2, [], fiveWindow));
        facts.Add(Number(FactKeys.MatchMomentumAway, total == 0 ? 0.5 : awayWeight / total, UnitShare, 2, [], fiveWindow));
    }

    private static void AddSideFacts(MatchStatsState state, string scope, string teamId, SideAccumulator side, string opponentId, List<Fact> facts)
    {
        string Key(string metric) => FactKeys.ForSide(scope, metric);

        facts.Add(Number(Key(FactKeys.Goals), side.Goals.Value, UnitGoals, 0, side.Goals.Ids));
        facts.Add(Number(Key(FactKeys.Xg), side.Xg.Value, UnitXg, 2, side.Xg.Ids));
        facts.Add(Number(Key(FactKeys.Shots), side.Shots.Value, UnitCount, 0, side.Shots.Ids));
        facts.Add(Number(Key(FactKeys.ShotsOnTarget), side.ShotsOnTarget.Value, UnitCount, 0, side.ShotsOnTarget.Ids));
        facts.Add(Number(Key(FactKeys.BigChances), side.BigChances.Value, UnitCount, 0, side.BigChances.Ids));
        facts.Add(Number(Key(FactKeys.Passes), side.Passes.Value, UnitCount, 0, side.Passes.Ids));
        facts.Add(Number(Key(FactKeys.PassesCompleted), side.PassesCompleted.Value, UnitCount, 0, side.PassesCompleted.Ids));
        facts.Add(Number(Key(FactKeys.PassCompletion), Ratio(side.PassesCompleted.Value, side.Passes.Value), UnitShare, 2, side.Passes.Ids));
        facts.Add(Number(Key(FactKeys.PassDifficultyAvg), Ratio(side.DifficultySum, side.Passes.Value), UnitDifficulty, 2, side.Passes.Ids));
        facts.Add(Number(Key(FactKeys.ProgressivePasses), side.ProgressivePasses.Value, UnitCount, 0, side.ProgressivePasses.Ids));
        facts.Add(Number(Key(FactKeys.FinalThirdEntries), side.FinalThirdEntries.Value, UnitCount, 0, side.FinalThirdEntries.Ids));

        var possessionTotal = state.Home.PossessionMs + state.Away.PossessionMs;
        facts.Add(Number(Key(FactKeys.PossessionShare), possessionTotal == 0 ? 0.5 : side.PossessionMs / possessionTotal, UnitShare, 2, []));
        facts.Add(Number(Key(FactKeys.Corners), side.Corners.Value, UnitCount, 0, side.Corners.Ids));
        facts.Add(Number(Key(FactKeys.Fouls), side.Fouls.Value, UnitCount, 0, side.Fouls.Ids));
        facts.Add(Number(Key(FactKeys.Yellows), side.Yellows.Value, UnitCount, 0, side.Yellows.Ids));
        facts.Add(Number(Key(FactKeys.Reds), side.Reds.Value, UnitCount, 0, side.Reds.Ids));
        facts.Add(Number(Key(FactKeys.Pressures), side.Pressures.Value, UnitCount, 0, side.Pressures.Ids));

        var window = state.Window.Items.ToList();
        var pressures = window.Count(e => e.Type == EventType.Pressure && e.TeamId == teamId);
        var opponentPasses = window.Count(e => e.Type == EventType.Pass && e.TeamId == opponentId);
        facts.Add(Number(Key(FactKeys.PressingIntensity), Ratio(pressures, opponentPasses), UnitPerPass, 2, [], WindowOf(window, state.AsOfSeq)));

        facts.Add(Number(Key(FactKeys.DistanceKm), side.DistanceM.Value / 1000, UnitKm, 2, side.DistanceM.Ids));
        var fastest = Metric.Empty;
        foreach (var player in state.Players.Values)
        {
            if (player.TeamId == teamId && player.TopSpeedKph.Value > fastest.Value)
            {
                fastest = player.TopSpeedKph;
            }
        }

        facts.Add(Number(Key(FactKeys.TopSpeedKph), fastest.Value, UnitKph, 1, fastest.Ids));
    }

    private static void AddPlayerFacts(MatchStatsState state, PlayerAccumulator p, List<Fact> facts)
    {
        string Key(string metric) => FactKeys.ForPlayer(p.PlayerId, metric);

        facts.Add(Number(Key(FactKeys.Passes), p.Passes.Value, UnitCount, 0, p.Passes.Ids));
        facts.Add(Number(Key(FactKeys.PassesCompleted), p.PassesCompleted.Value, UnitCount, 0, p.PassesCompleted.Ids));
        facts.Add(Number(Key(FactKeys.PassCompletion), Ratio(p.PassesCompleted.Value, p.Passes.Value), UnitShare, 2, p.Passes.Ids));
        facts.Add(Number(Key(FactKeys.ProgressivePasses), p.ProgressivePasses.Value, UnitCount, 0, p.ProgressivePasses.Ids));
        facts.Add(Number(Key(FactKeys.Carries), p.Carries.Value, UnitCount, 0, p.Carries.Ids));
        facts.Add(Number(Key(FactKeys.DistanceKm), p.DistanceM.Value / 1000, UnitKm, 2, p.DistanceM.Ids));
        facts.Add(Number(Key(FactKeys.TopSpeedKph), p.TopSpeedKph.Value, UnitKph, 1, p.TopSpeedKph.Ids));
        facts.Add(Number(Key(FactKeys.Shots), p.Shots.Value, UnitCount, 0, p.Shots.Ids));
        facts.Add(Number(Key(FactKeys.Xg), p.Xg.Value, UnitXg, 2, p.Xg.Ids));
        facts.Add(Number(Key(FactKeys.Goals), p.Goals.Value, UnitGoals, 0, p.Goals.Ids));
        facts.Add(Number(Key(FactKeys.Assists), p.Assists.Value, UnitCount, 0, p.Assists.Ids));
        facts.Add(Number(Key(FactKeys.Pressures), p.Pressures.Value, UnitCount, 0, p.Pressures.Ids));
        facts.Add(Number(Key(FactKeys.Fouls), p.Fouls.Value, UnitCount, 0, p.Fouls.Ids));
        facts.Add(Number(Key(FactKeys.Cards), p.Cards.Value, UnitCount, 0, p.Cards.Ids));
        facts.Add(Number(Key(FactKeys.Minutes), p.MillisecondsPlayed(state.ClockMs) / 60_000, UnitMinutes, 0, []));
        facts.Add(Number(Key(FactKeys.Sprints), p.Sprints.Value, UnitCount, 0, p.Sprints.Ids));
        facts.Add(new Fact
        {
            Key = Key(FactKeys.Tags),
            Value = FactValue.Of(string.Join(',', p.TagNames)),
            Precision = 0,
            DerivedFrom = p.TagEvents.Ids,
        });
    }

    private static Fact Number(string key, double value, string? unit, int precision, IReadOnlyList<string> derivedFrom, FactWindow? window = null) =>
        new()
        {
            Key = key,
            Value = FactValue.Of(Math.Round(value, precision, MidpointRounding.AwayFromZero)),
            Unit = unit,
            Precision = precision,
            DerivedFrom = derivedFrom,
            Window = window,
        };

    private static double Ratio(double numerator, double denominator) => denominator == 0 ? 0 : numerator / denominator;

    private static FactWindow WindowOf(IReadOnlyList<MatchEvent> events, int asOfSeq) =>
        new(events.Count == 0 ? asOfSeq : events[0].Seq, asOfSeq);
}
