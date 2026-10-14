using Pitchside.Contracts;

namespace Pitchside.Stats.Tests;

/// <summary>Every metric on <see cref="TinyMatch"/>, against values worked out by hand from the event list.</summary>
public sealed class MetricDefinitionTests
{
    private static readonly MatchStatsEngine Engine = new();

    private static FactSheet At(int seq)
    {
        var state = Engine.Start(TinyMatch.Document);
        foreach (var e in TinyMatch.Events.Take(seq))
        {
            state = Engine.Apply(state, e).State;
        }

        return Engine.Snapshot(state);
    }

    private static string H(string metric) => FactKeys.ForSide(FactKeys.HomeScope, metric);

    private static string A(string metric) => FactKeys.ForSide(FactKeys.AwayScope, metric);

    private static string P(string id, string metric) => FactKeys.ForPlayer(id, metric);

    [Fact]
    public void Pre_match_sheet_is_neutral()
    {
        var sheet = At(0);
        sheet.AsOfSeq.ShouldBe(0);
        sheet.TextOf(FactKeys.MatchState).ShouldBe(FactKeys.StatePreMatch);
        sheet.NumberOf(FactKeys.MatchClockMinute).ShouldBe(0);
        sheet.NumberOf(FactKeys.MatchMomentumHome).ShouldBe(0.5);
        sheet.NumberOf(H(FactKeys.PossessionShare)).ShouldBe(0.5);
        sheet.Facts.ShouldNotContain(f => f.Key.StartsWith(FactKeys.PlayerScope + ".", StringComparison.Ordinal));
    }

    [Fact]
    public void Match_scope_at_full_time()
    {
        var sheet = At(20);
        sheet.TextOf(FactKeys.MatchState).ShouldBe(FactKeys.StateFullTime);
        sheet.NumberOf(FactKeys.MatchPeriod).ShouldBe(2);
        sheet.NumberOf(FactKeys.MatchClockMinute).ShouldBe(48);
        sheet.NumberOf(FactKeys.MatchScoreHome).ShouldBe(1);
        sheet.NumberOf(FactKeys.MatchScoreAway).ShouldBe(0);
        sheet.Find(FactKeys.MatchScoreHome)!.DerivedFrom.ShouldBe([TinyMatch.Id(7)]);
        sheet.NumberOf(FactKeys.MatchPossessions).ShouldBe(4);

        // The second half's window holds only the kick-off, a substitution and full time.
        sheet.NumberOf(FactKeys.MatchRhythm).ShouldBe(0);
        sheet.NumberOf(FactKeys.MatchControl).ShouldBe(0.5);
        sheet.NumberOf(FactKeys.MatchMomentumHome).ShouldBe(0.5);
        sheet.Find(FactKeys.MatchControl)!.Window.ShouldBe(new FactWindow(18, 20));
    }

    [Fact]
    public void Rolling_metrics_late_in_the_first_half()
    {
        var sheet = At(16);
        sheet.NumberOf(FactKeys.MatchClockMinute).ShouldBe(2);

        // Possessions 1 to 4 over 103 s of play: 4 / 1.7167 min.
        sheet.NumberOf(FactKeys.MatchRhythm).ShouldBe(2.33);

        // Open play: passes 2, 5, 8, 9, pressure 3, carry 4, shots 6, 16, turnover 10, foul 11 = 10.
        // Controlled: completed passes 2, 5, 8 and carry 4 = 4.
        sheet.NumberOf(FactKeys.MatchControl).ShouldBe(0.4);

        // Home: shot 6 (3 + 4 + 10 x 0.4 = 11), carry 4 into the final third (1), corner 15 (2), shot 16 (3 + 1) = 18.
        sheet.NumberOf(FactKeys.MatchMomentumHome).ShouldBe(1);
        sheet.NumberOf(FactKeys.MatchMomentumAway).ShouldBe(0);
        sheet.Find(FactKeys.MatchMomentumHome)!.Window.ShouldBe(new FactWindow(1, 16));

        // Away pressed once against two home passes; home never pressed.
        sheet.NumberOf(A(FactKeys.PressingIntensity)).ShouldBe(0.5);
        sheet.NumberOf(H(FactKeys.PressingIntensity)).ShouldBe(0);
    }

    [Fact]
    public void Side_scope_at_full_time()
    {
        var sheet = At(20);
        sheet.NumberOf(H(FactKeys.Goals)).ShouldBe(1);
        sheet.NumberOf(H(FactKeys.Xg)).ShouldBe(0.5);
        sheet.NumberOf(H(FactKeys.Shots)).ShouldBe(2);
        sheet.NumberOf(H(FactKeys.ShotsOnTarget)).ShouldBe(1);
        sheet.NumberOf(H(FactKeys.BigChances)).ShouldBe(1);
        sheet.NumberOf(H(FactKeys.Passes)).ShouldBe(2);
        sheet.NumberOf(H(FactKeys.PassesCompleted)).ShouldBe(2);
        sheet.NumberOf(H(FactKeys.PassCompletion)).ShouldBe(1);
        sheet.NumberOf(H(FactKeys.PassDifficultyAvg)).ShouldBe(0.4);
        sheet.NumberOf(H(FactKeys.ProgressivePasses)).ShouldBe(2);
        sheet.NumberOf(H(FactKeys.FinalThirdEntries)).ShouldBe(1);
        sheet.Find(H(FactKeys.FinalThirdEntries))!.DerivedFrom.ShouldBe([TinyMatch.Id(4)]);
        sheet.NumberOf(H(FactKeys.Corners)).ShouldBe(1);
        sheet.NumberOf(H(FactKeys.DistanceKm)).ShouldBe(0.38);
        sheet.NumberOf(H(FactKeys.TopSpeedKph)).ShouldBe(31);
        sheet.Find(H(FactKeys.TopSpeedKph))!.DerivedFrom.ShouldBe([TinyMatch.Id(13)]);

        sheet.NumberOf(A(FactKeys.Passes)).ShouldBe(2);
        sheet.NumberOf(A(FactKeys.PassCompletion)).ShouldBe(0.5);
        sheet.NumberOf(A(FactKeys.ProgressivePasses)).ShouldBe(0);
        sheet.NumberOf(A(FactKeys.Fouls)).ShouldBe(1);
        sheet.NumberOf(A(FactKeys.Yellows)).ShouldBe(1);
        sheet.NumberOf(A(FactKeys.Reds)).ShouldBe(0);
        sheet.NumberOf(A(FactKeys.Pressures)).ShouldBe(1);
        sheet.NumberOf(A(FactKeys.DistanceKm)).ShouldBe(0.33);
        sheet.NumberOf(A(FactKeys.TopSpeedKph)).ShouldBe(34);
    }

    [Fact]
    public void Possession_share_goes_to_whoever_had_the_ball()
    {
        // Home holds the ball for 2,752 s (including first-half stoppage), away for 128 s.
        var sheet = At(20);
        sheet.NumberOf(H(FactKeys.PossessionShare)).ShouldBe(0.96);
        sheet.NumberOf(A(FactKeys.PossessionShare)).ShouldBe(0.04);
    }

    [Fact]
    public void Player_scope_at_full_time()
    {
        var sheet = At(20);
        sheet.NumberOf(P("HOM-07", FactKeys.Goals)).ShouldBe(1);
        sheet.NumberOf(P("HOM-07", FactKeys.Shots)).ShouldBe(1);
        sheet.NumberOf(P("HOM-07", FactKeys.Xg)).ShouldBe(0.4);
        sheet.NumberOf(P("HOM-07", FactKeys.TopSpeedKph)).ShouldBe(31);
        sheet.NumberOf(P("HOM-07", FactKeys.Sprints)).ShouldBe(1);
        sheet.NumberOf(P("HOM-07", FactKeys.DistanceKm)).ShouldBe(0.13);
        sheet.TextOf(P("HOM-07", FactKeys.Tags)).ShouldBe("shot.fast,speed.30");

        sheet.NumberOf(P("HOM-09", FactKeys.Assists)).ShouldBe(1);
        sheet.NumberOf(P("HOM-09", FactKeys.Carries)).ShouldBe(1);
        sheet.NumberOf(P("HOM-09", FactKeys.ProgressivePasses)).ShouldBe(1);
        sheet.NumberOf(P("HOM-09", FactKeys.Sprints)).ShouldBe(1);
        sheet.NumberOf(P("HOM-09", FactKeys.TopSpeedKph)).ShouldBe(27);
        sheet.TextOf(P("HOM-09", FactKeys.Tags)).ShouldBe("pass.hard");

        // 46 minutes in the first half, one in the second before going off.
        sheet.NumberOf(P("HOM-09", FactKeys.Minutes)).ShouldBe(47);
        sheet.NumberOf(P("HOM-12", FactKeys.Minutes)).ShouldBe(1);
        sheet.NumberOf(P("HOM-01", FactKeys.Minutes)).ShouldBe(48);

        sheet.NumberOf(P("AWY-05", FactKeys.Passes)).ShouldBe(1);
        sheet.NumberOf(P("AWY-05", FactKeys.PassCompletion)).ShouldBe(0);
        sheet.NumberOf(P("AWY-05", FactKeys.Fouls)).ShouldBe(1);
        sheet.NumberOf(P("AWY-05", FactKeys.Cards)).ShouldBe(1);
        sheet.NumberOf(P("AWY-05", FactKeys.Pressures)).ShouldBe(1);
        sheet.TextOf(P("AWY-10", FactKeys.Tags)).ShouldBe("speed.30,speed.33");

        sheet.Find(P("AWY-14", FactKeys.Minutes)).ShouldBeNull();
    }

    [Fact]
    public void Incremental_equals_batch_on_the_tiny_match()
    {
        for (var seq = 0; seq <= TinyMatch.Events.Count; seq++)
        {
            At(seq).ShouldEqualSheet(BatchStats.Compute(TinyMatch.Document, TinyMatch.Events.Take(seq).ToList()));
        }
    }
}
