using Microsoft.Extensions.DependencyInjection;
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

    private static async Task<IMatchStore> SeededAsync(IMatchStore store)
    {
        await store.SaveMomentAsync(Goal, CancellationToken.None);
        await store.SaveFactSheetAsync(new FactSheet("MER-7", 120, 1, 600_000,
        [
            new Fact { Key = "match.score.home", Value = FactValue.Of(1), Unit = "goals", Precision = 0, DerivedFrom = ["MER-7-00110"] },
        ]), CancellationToken.None);
        return store;
    }

    private static GroundednessBatcher Batcher(IEvaluationProvider judge, IMatchStore store) =>
        new(judge, store, new NullMatchBroadcaster(), Options.Create(new EvaluationOptions { BatchSeconds = 1 }), NullLogger<GroundednessBatcher>.Instance);

    private static async Task<double?> ScoreOfAsync(IMatchStore store, string persona)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        double? score = null;
        while (score is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            score = (await store.GetNarrativesAsync("MER-7", CancellationToken.None)).SingleOrDefault(n => n.Persona == persona)?.Evaluation.Groundedness;
        }

        return score;
    }

    [Fact]
    public async Task A_judge_that_times_out_on_one_narrative_never_stops_the_batcher()
    {
        var store = await SeededAsync(new InMemoryMatchStore());
        using var batcher = Batcher(new TimingOutJudge(), store);
        await batcher.StartAsync(CancellationToken.None);

        batcher.Enqueue(For(Personas.Analyst));
        batcher.Enqueue(For(Personas.Casual));

        (await ScoreOfAsync(store, Personas.Casual)).ShouldBe(0.5);
        batcher.ExecuteTask!.IsCompleted.ShouldBeFalse();
        await batcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_save_that_fails_is_retried_without_a_second_judgement()
    {
        var store = await SeededAsync(new FlakyStore(new InMemoryMatchStore(), failFirstSave: true));
        var judge = new CountingJudge();
        using var batcher = Batcher(judge, store);
        await batcher.StartAsync(CancellationToken.None);

        batcher.Enqueue(For(Personas.Casual));

        (await ScoreOfAsync(store, Personas.Casual)).ShouldBe(0.5);
        judge.Calls.ShouldBe(1);
        await batcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_narrative_queued_again_on_a_later_tick_is_judged_once()
    {
        var store = await SeededAsync(new InMemoryMatchStore());
        var judge = new CountingJudge();
        using var batcher = Batcher(judge, store);
        await batcher.StartAsync(CancellationToken.None);

        batcher.Enqueue(For(Personas.Casual));
        (await ScoreOfAsync(store, Personas.Casual)).ShouldBe(0.5);

        // The same unscored narrative again, as when the start-up scan and the live publisher both queue it.
        batcher.Enqueue(For(Personas.Casual));
        await Task.Delay(2_500);

        judge.Calls.ShouldBe(1);
        (await ScoreOfAsync(store, Personas.Casual)).ShouldBe(0.5);
        await batcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_start_up_scan_that_fails_is_run_again_on_the_next_tick()
    {
        var store = await SeededAsync(new FlakyStore(new InMemoryMatchStore(), failFirstList: true));
        await store.SaveMatchAsync(new StoredMatch(Pitchside.Engine.MatchSimulator.Simulate(7).Document, MatchStatus.Completed, 0, DateTimeOffset.UnixEpoch), CancellationToken.None);
        await store.SaveNarrativeAsync(For(Personas.Casual), CancellationToken.None);
        var judge = new CountingJudge();
        using var batcher = Batcher(judge, store);
        await batcher.StartAsync(CancellationToken.None);

        (await ScoreOfAsync(store, Personas.Casual)).ShouldBe(0.5);
        judge.Calls.ShouldBe(1);
        await batcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Verified_narratives_stored_without_a_score_are_scored_after_a_restart()
    {
        var store = await SeededAsync(new InMemoryMatchStore());
        await store.SaveMatchAsync(new StoredMatch(Pitchside.Engine.MatchSimulator.Simulate(7).Document, MatchStatus.Completed, 0, DateTimeOffset.UnixEpoch), CancellationToken.None);
        await store.SaveNarrativeAsync(For(Personas.Casual), CancellationToken.None);
        await store.SaveNarrativeAsync(For(Personas.Analyst) with { Status = NarrativeStatus.Rejected }, CancellationToken.None);
        var judge = new CountingJudge();

        // A fresh batcher, as after a restart: nothing is queued, so only its start-up scan can find the narrative.
        using var batcher = Batcher(judge, store);
        await batcher.StartAsync(CancellationToken.None);

        (await ScoreOfAsync(store, Personas.Casual)).ShouldBe(0.5);
        judge.Calls.ShouldBe(1);
        await batcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_saved_narrative_whose_broadcast_fails_is_still_scored()
    {
        await using var factory = new ConfiguredApiFactory(new Dictionary<string, string?> { ["Evaluation:BatchSeconds"] = "1" });
        var services = factory.Services;
        var store = await SeededAsync(services.GetRequiredService<IMatchStore>());
        var publisher = new StoreAndBroadcastNarrativePublisher(
            store, new NarrativeFailingBroadcaster(), services.GetRequiredService<OverlayCoordinator>(), services.GetRequiredService<GroundednessBatcher>());

        await Should.ThrowAsync<InvalidOperationException>(() => publisher.PublishAsync(For(Personas.Casual), CancellationToken.None));

        (await ScoreOfAsync(store, Personas.Casual)).ShouldNotBeNull();
    }
}
