using System.Text.Json;
using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments.Tests;

/// <summary>
/// Seed 7 produces a stable list of moments, committed as a golden master. A deliberate change to a rule
/// regenerates it with <c>dotnet run --project src/Pitchside.Cli -- moments --seed 7</c> in a reviewed pull request.
/// </summary>
public sealed class SampleMatchMomentsTests
{
    private static readonly IReadOnlyList<Moment> Moments = MatchMoments.Detect(SampleMatch.Document, SampleMatch.Events);

    [Fact]
    public void Seed_7_moments_equal_the_golden_master()
    {
        var expected = File.ReadAllLines(RepositoryRoot.Combine("tests", "Pitchside.Moments.Tests", "golden", "seed-7.moments.jsonl"))
            .Where(line => line.Length > 0).ToList();
        Moments.Select(m => JsonSerializer.Serialize(m, ContractJson.Options)).ToList().ShouldBe(expected);
    }

    [Fact]
    public void Every_goal_produces_exactly_one_goal_moment()
    {
        var goals = SampleMatch.Events.Where(e => e.Type == EventType.Goal).Select(e => e.Seq).ToList();
        Moments.Where(m => m.Type == MomentTypes.Goal).Select(m => m.TriggerSeq).ToList().ShouldBe(goals);
    }

    [Fact]
    public void The_sample_has_between_twelve_and_forty_moments_with_unique_ids()
    {
        Moments.Count.ShouldBeInRange(12, 40);
        Moments.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(Moments.Count);
    }

    [Fact]
    public void Every_evidence_key_resolves_in_the_sheet_the_moment_names()
    {
        var engine = new MatchStatsEngine();
        var state = engine.Start(SampleMatch.Document);
        var sheets = new Dictionary<int, FactSheet>();
        foreach (var e in SampleMatch.Events)
        {
            state = engine.Apply(state, e).State;
            if (Moments.Any(m => m.FactSheetSeq == e.Seq))
            {
                sheets[e.Seq] = engine.Snapshot(state);
            }
        }

        var eventIds = SampleMatch.Events.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var moment in Moments)
        {
            moment.FactSheetSeq.ShouldBe(moment.TriggerSeq);
            moment.Window.FromSeq.ShouldBeLessThanOrEqualTo(moment.Window.ToSeq);
            moment.Id.ShouldBe(Moment.IdFor(moment.MatchId, moment.TriggerSeq, moment.Type));
            foreach (var key in moment.Evidence.FactKeys)
            {
                sheets[moment.FactSheetSeq].Find(key).ShouldNotBeNull($"{moment.Id} cites {key}");
            }

            foreach (var id in moment.Evidence.EventIds)
            {
                eventIds.ShouldContain(id);
            }
        }
    }

    [Fact]
    public void Detection_is_deterministic()
    {
        var again = MatchMoments.Detect(SampleMatch.Document, SampleMatch.Events);
        again.Select(m => JsonSerializer.Serialize(m, ContractJson.Options))
            .ShouldBe(Moments.Select(m => JsonSerializer.Serialize(m, ContractJson.Options)));
    }
}
