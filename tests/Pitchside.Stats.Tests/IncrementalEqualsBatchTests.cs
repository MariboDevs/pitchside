using Pitchside.Contracts;

namespace Pitchside.Stats.Tests;

/// <summary>The incremental engine and the independent batch oracle agree on the sample match.</summary>
public sealed class IncrementalEqualsBatchTests
{
    private readonly MatchStatsEngine _engine = new();

    [Fact]
    public void Full_time_sheet_equals_the_batch_sheet()
    {
        var state = Run(SampleMatch.Events.Count);
        _engine.Snapshot(state).ShouldEqualSheet(BatchStats.Compute(SampleMatch.Document, SampleMatch.Events));
    }

    [Fact]
    public void Sheets_along_the_match_equal_the_batch_sheets()
    {
        var events = SampleMatch.Events;
        var state = _engine.Start(SampleMatch.Document);
        _engine.Snapshot(state).ShouldEqualSheet(BatchStats.Compute(SampleMatch.Document, []));
        for (var i = 0; i < events.Count; i++)
        {
            state = _engine.Apply(state, events[i]).State;
            if (i % 37 == 0 || events[i].Type is EventType.HalfEnd or EventType.KickOff or EventType.Substitution or EventType.Goal)
            {
                _engine.Snapshot(state).ShouldEqualSheet(BatchStats.Compute(SampleMatch.Document, events.Take(i + 1).ToList()));
            }
        }
    }

    [Fact]
    public void Tags_raised_incrementally_equal_the_batch_tags()
    {
        var state = _engine.Start(SampleMatch.Document);
        var raised = new List<Tag>();
        foreach (var e in SampleMatch.Events)
        {
            var step = _engine.Apply(state, e);
            raised.AddRange(step.Tags);
            state = step.State;
        }

        var expected = state.Players.Keys.SelectMany(id => BatchStats.Tags(SampleMatch.Events, id)).ToList();
        raised.OrderBy(t => t.PlayerId, StringComparer.Ordinal).ThenBy(t => t.EventId, StringComparer.Ordinal).ThenBy(t => t.Name, StringComparer.Ordinal).ToList()
            .ShouldBe(expected.OrderBy(t => t.PlayerId, StringComparer.Ordinal).ThenBy(t => t.EventId, StringComparer.Ordinal).ThenBy(t => t.Name, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Full_time_sheet_holds_every_catalogue_key()
    {
        var sheet = _engine.Snapshot(Run(SampleMatch.Events.Count));
        string[] match =
        [
            FactKeys.MatchClockMinute, FactKeys.MatchPeriod, FactKeys.MatchState, FactKeys.MatchScoreHome, FactKeys.MatchScoreAway,
            FactKeys.MatchPossessions, FactKeys.MatchRhythm, FactKeys.MatchControl, FactKeys.MatchMomentumHome, FactKeys.MatchMomentumAway,
        ];
        string[] side =
        [
            FactKeys.Goals, FactKeys.Xg, FactKeys.Shots, FactKeys.ShotsOnTarget, FactKeys.BigChances, FactKeys.Passes,
            FactKeys.PassesCompleted, FactKeys.PassCompletion, FactKeys.PassDifficultyAvg, FactKeys.ProgressivePasses,
            FactKeys.FinalThirdEntries, FactKeys.PossessionShare, FactKeys.Corners, FactKeys.Fouls, FactKeys.Yellows, FactKeys.Reds,
            FactKeys.Pressures, FactKeys.PressingIntensity, FactKeys.DistanceKm, FactKeys.TopSpeedKph,
        ];
        string[] player =
        [
            FactKeys.Passes, FactKeys.PassesCompleted, FactKeys.PassCompletion, FactKeys.ProgressivePasses, FactKeys.Carries,
            FactKeys.DistanceKm, FactKeys.TopSpeedKph, FactKeys.Shots, FactKeys.Xg, FactKeys.Goals, FactKeys.Assists,
            FactKeys.Pressures, FactKeys.Fouls, FactKeys.Cards, FactKeys.Minutes, FactKeys.Sprints, FactKeys.Tags,
        ];
        var keys = sheet.Facts.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in match)
        {
            keys.ShouldContain(key);
        }

        foreach (var scope in new[] { FactKeys.HomeScope, FactKeys.AwayScope })
        {
            foreach (var metric in side)
            {
                keys.ShouldContain(FactKeys.ForSide(scope, metric));
            }
        }

        var starter = SampleMatch.Document.Home.Starters[5].Id;
        foreach (var metric in player)
        {
            keys.ShouldContain(FactKeys.ForPlayer(starter, metric));
        }

        sheet.Facts.Count.ShouldBeGreaterThanOrEqualTo(120);
        keys.Count.ShouldBe(sheet.Facts.Count);
    }

    [Fact]
    public void Full_time_sheet_agrees_with_the_full_time_event()
    {
        var sheet = _engine.Snapshot(Run(SampleMatch.Events.Count));
        var fullTime = SampleMatch.Events.Single(e => e.Type == EventType.FullTime);
        sheet.NumberOf(FactKeys.MatchScoreHome).ShouldBe(fullTime.HomeScore!.Value);
        sheet.NumberOf(FactKeys.MatchScoreAway).ShouldBe(fullTime.AwayScore!.Value);
        sheet.TextOf(FactKeys.MatchState).ShouldBe(FactKeys.StateFullTime);
        sheet.NumberOf(FactKeys.ForSide(FactKeys.HomeScope, FactKeys.PossessionShare))
            .ShouldBe(1 - sheet.NumberOf(FactKeys.ForSide(FactKeys.AwayScope, FactKeys.PossessionShare)), 0.0101);
        sheet.Facts.ShouldAllBe(f => f.DerivedFrom.Count <= 50);
    }

    private MatchStatsState Run(int count)
    {
        var state = _engine.Start(SampleMatch.Document);
        foreach (var e in SampleMatch.Events.Take(count))
        {
            state = _engine.Apply(state, e).State;
        }

        return state;
    }
}
