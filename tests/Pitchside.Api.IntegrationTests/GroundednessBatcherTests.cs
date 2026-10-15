using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pitchside.Api.Broadcast;
using Pitchside.Contracts;
using Pitchside.Evaluation;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.IntegrationTests;

public sealed class GroundednessBatcherTests
{
    private static readonly Moment Goal = new()
    {
        Id = "MER-7:M:0120:goal",
        MatchId = "MER-7",
        Type = "goal",
        Period = 1,
        ClockMs = 600_000,
        TriggerSeq = 120,
        FactSheetSeq = 120,
        Priority = 100,
        Window = new MomentWindow(100, 120),
        SideId = "AMZ",
        PlayerIds = [],
        Headline = "Goal",
        Evidence = new MomentEvidence(["MER-7-00110"], ["match.score.home"]),
    };

    private static Narrative For(string persona) => new()
    {
        Id = Narrative.IdFor(Goal.Id, persona, Languages.English),
        MatchId = "MER-7",
        MomentId = Goal.Id,
        Persona = persona,
        Language = Languages.English,
        FactSheetSeq = 120,
        Sentences = [new NarrativeSentence("Amanzi lead [[match.score.home]].", ["match.score.home"])],
        RenderedText = "Amanzi lead 1.",
        Status = NarrativeStatus.Verified,
        Provenance = new NarrativeProvenance("scripted", "scripted", $"persona-{persona}-v1", 1, DateTimeOffset.UnixEpoch, false),
        Evaluation = new NarrativeEvaluation(1, 1, null),
    };

    [Fact]
    public async Task A_judge_that_times_out_on_one_narrative_never_stops_the_batcher()
    {
        var store = new InMemoryMatchStore();
        await store.SaveMomentAsync(Goal, CancellationToken.None);
        await store.SaveFactSheetAsync(new FactSheet("MER-7", 120, 1, 600_000,
        [
            new Fact { Key = "match.score.home", Value = FactValue.Of(1), Unit = "goals", Precision = 0, DerivedFrom = ["MER-7-00110"] },
        ]), CancellationToken.None);
        using var batcher = new GroundednessBatcher(
            new TimingOutJudge(), store, new NullMatchBroadcaster(), Options.Create(new EvaluationOptions { BatchSeconds = 1 }), NullLogger<GroundednessBatcher>.Instance);
        await batcher.StartAsync(CancellationToken.None);

        batcher.Enqueue(For(Personas.Analyst));
        batcher.Enqueue(For(Personas.Casual));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        double? casual = null;
        while (casual is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            casual = (await store.GetNarrativesAsync("MER-7", CancellationToken.None)).SingleOrDefault(n => n.Persona == Personas.Casual)?.Evaluation.Groundedness;
        }

        casual.ShouldBe(0.5);
        batcher.ExecuteTask!.IsCompleted.ShouldBeFalse();
        await batcher.StopAsync(CancellationToken.None);
    }
}
