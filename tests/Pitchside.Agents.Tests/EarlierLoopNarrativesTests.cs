using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Speech;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Runtime.Audit;
using Pitchside.Runtime.MomentSink;
using Pitchside.Runtime.Recaps;
using Pitchside.Runtime.Storage;

namespace Pitchside.Agents.Tests;

/// <summary>A later demo loop replays what the crew wrote for the same moment in the loop before, with no model call.</summary>
public sealed class EarlierLoopNarrativesTests
{
    private const string Loop1 = "MER-7";
    private const string Loop2 = "MER-7-2";

    private static readonly DetectedMoment Goal = Matches.Moment(7, "MER-7:M:0324:goal");
    private static readonly DetectedMoment FullTime = Matches.Moments(7).Single(m => m.Moment.Type == "full_time");

    private readonly InMemoryMatchStore _store = new();
    private readonly InMemoryAuditStore _audit = new();
    private readonly InMemoryRecapStore _recaps = new();

    [Fact]
    public async Task A_later_loop_publishes_the_loop_befores_narratives_under_its_own_ids()
    {
        var earlier = await WriteLoop1Async(Goal);
        var log = new InMemoryNarrativeLog();

        (await Replays().TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeTrue();

        log.Narratives.Count.ShouldBe(earlier.Count);
        log.Narratives[0].Persona.ShouldBe(Personas.Base);
        log.Narratives[0].Language.ShouldBe(Languages.English);
        foreach (var copy in log.Narratives)
        {
            var original = earlier.Single(n => n.Id == copy.Id.Replace(Loop2 + ":", Loop1 + ":", StringComparison.Ordinal));
            copy.MatchId.ShouldBe(Loop2);
            copy.MomentId.ShouldBe("MER-7-2:M:0324:goal");
            copy.Id.ShouldStartWith("MER-7-2:M:0324:goal:");
            copy.RenderedText.ShouldBe(original.RenderedText);
            copy.Sentences.ShouldBe(original.Sentences);
            copy.Status.ShouldBe(original.Status);
            copy.Evaluation.ShouldBe(original.Evaluation);
            copy.Provenance.ShouldBe(original.Provenance with { Provider = original.Provenance.Provider + EarlierLoopNarratives.ReplaySuffix });
        }

        var calls = await _audit.ListAsync(Loop2, null, 10, CancellationToken.None);
        calls.ShouldHaveSingleItem().MomentId.ShouldBe("MER-7-2:M:0324:goal");
        calls[0].Id.ShouldBe(AuditId);
    }

    [Fact]
    public async Task The_runner_calls_no_model_for_a_moment_it_replays()
    {
        await WriteLoop1Async(Goal);
        var provider = new FakeChatProvider((_, _) => throw new InvalidOperationException("No model may be called for a replayed moment."));
        var sink = new ChannelMomentSink();
        var log = new InMemoryNarrativeLog();
        var options = Options.Create(new AgentsOptions());
        var runner = new MomentWorkflowRunner(sink, Crew.Workflow(provider), log, options, NullLogger<MomentWorkflowRunner>.Instance, earlierLoops: Replays());

        await runner.StartAsync(CancellationToken.None);
        await sink.EnqueueAsync(InLoop2(Goal), CancellationToken.None);
        sink.Complete();
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));

        provider.Requests.ShouldBeEmpty();
        log.Narratives.ShouldNotBeEmpty();
        log.Narratives.ShouldAllBe(n => n.MatchId == Loop2);
    }

    [Fact]
    public async Task A_replay_of_a_replay_is_marked_once()
    {
        await WriteLoop1Async(Goal);
        var loop2 = new InMemoryNarrativeLog();
        await Replays().TryPublishAsync(InLoop2(Goal), loop2.PublishAsync, CancellationToken.None);
        foreach (var narrative in loop2.Narratives)
        {
            await _store.SaveNarrativeAsync(narrative, CancellationToken.None);
        }

        var loop3 = new InMemoryNarrativeLog();
        (await Replays().TryPublishAsync(InLoop(Goal, "MER-7-3"), loop3.PublishAsync, CancellationToken.None)).ShouldBeTrue();

        loop3.Narratives.ShouldAllBe(n => n.MatchId == "MER-7-3" && n.Provenance.Provider == ScriptedChatProvider.ProviderName + EarlierLoopNarratives.ReplaySuffix);
    }

    [Fact]
    public async Task The_first_loop_and_an_unlooped_match_are_written_by_the_crew()
    {
        await WriteLoop1Async(Goal);
        var log = new InMemoryNarrativeLog();

        (await Replays().TryPublishAsync(Goal.WorkItem, log.PublishAsync, CancellationToken.None)).ShouldBeFalse();
        (await Replays().TryPublishAsync(InLoop(Goal, "MER-8-2"), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();
        (await Replays(new AgentsOptions { ReplayEarlierLoops = false }).TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();
        (await Replays(store: null).TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();
        log.Narratives.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_moment_the_loop_before_has_no_narratives_for_is_written_by_the_crew()
    {
        var log = new InMemoryNarrativeLog();

        (await Replays().TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();

        log.Narratives.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_store_that_cannot_be_read_leaves_the_moment_to_the_crew()
    {
        var log = new InMemoryNarrativeLog();

        (await Replays(new FailingMatchStore()).TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();

        log.Narratives.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_sheet_that_renders_differently_is_written_by_the_crew()
    {
        var earlier = await WriteLoop1Async(Goal);
        var cited = earlier.SelectMany(n => n.Sentences).SelectMany(s => s.Citations).First();
        var item = InLoop2(Goal);
        var changed = item with
        {
            FactSheet = item.FactSheet with
            {
                Facts = [.. item.FactSheet.Facts.Select(f => f.Key == cited && f.Value.Number is { } v ? f with { Value = FactValue.Of(v + 1) } : f)],
            },
        };
        var log = new InMemoryNarrativeLog();

        (await Replays().TryPublishAsync(changed, log.PublishAsync, CancellationToken.None)).ShouldBeFalse();
        (await Replays().TryPublishAsync(item with { FactSheet = item.FactSheet with { AsOfSeq = item.FactSheet.AsOfSeq + 1 } }, log.PublishAsync, CancellationToken.None)).ShouldBeFalse();

        log.Narratives.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_moment_the_loop_before_only_has_a_fallback_for_is_written_by_the_crew()
    {
        var earlier = await WriteLoop1Async(Goal);
        foreach (var narrative in earlier)
        {
            await _store.SaveNarrativeAsync(narrative with { Status = NarrativeStatus.Fallback }, CancellationToken.None);
        }

        var log = new InMemoryNarrativeLog();

        (await Replays().TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();

        log.Narratives.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_moment_the_loop_before_lacks_a_variant_for_is_written_by_the_crew()
    {
        await WriteLoop1Async(Goal, keep: n => !IsCasualSpanish(n));
        var log = new InMemoryNarrativeLog();

        (await Replays().TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();

        log.Narratives.ShouldBeEmpty();
    }

    [Fact]
    public async Task Variants_the_options_no_longer_write_are_not_replayed()
    {
        await WriteLoop1Async(Goal);
        var log = new InMemoryNarrativeLog();
        var fewer = new AgentsOptions { Languages = [Languages.English, Languages.Afrikaans] };

        (await Replays(fewer).TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();

        log.Narratives.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_loop_read_before_its_moment_was_complete_is_read_again_a_minute_later()
    {
        var earlier = await WriteLoop1Async(Goal, keep: n => !IsCasualSpanish(n));
        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var replays = Replays(time: clock);
        var log = new InMemoryNarrativeLog();
        (await replays.TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();

        // The variant still in flight is stored; the loop held is read again only once it is a minute old.
        await _store.SaveNarrativeAsync(earlier.Single(IsCasualSpanish), CancellationToken.None);
        (await replays.TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeFalse();
        clock.Advance(EarlierLoopNarratives.RefreshAfter);

        (await replays.TryPublishAsync(InLoop2(Goal), log.PublishAsync, CancellationToken.None)).ShouldBeTrue();
        log.Narratives.Count.ShouldBe(earlier.Count);
    }

    [Fact]
    public async Task A_publish_that_fails_does_not_stop_the_rest_and_is_tried_again()
    {
        var earlier = await WriteLoop1Async(Goal);
        var publisher = new FailOncePublisher();

        (await Replays().TryPublishAsync(InLoop2(Goal), publisher.PublishAsync, CancellationToken.None)).ShouldBeTrue();

        publisher.Narratives.Select(n => n.Id).ShouldBe(
            earlier.Select(n => n.Id.Replace(Loop1 + ":", Loop2 + ":", StringComparison.Ordinal)), ignoreOrder: true);
    }

    [Fact]
    public async Task The_full_time_recap_is_copied_with_the_narratives()
    {
        await WriteLoop1Async(FullTime, Crew.Recaps(_recaps));
        var log = new InMemoryNarrativeLog();

        (await Replays().TryPublishAsync(InLoop2(FullTime), log.PublishAsync, CancellationToken.None)).ShouldBeTrue();

        var original = (await _recaps.GetAsync(Loop1, Languages.English, CancellationToken.None))!;
        var copy = (await _recaps.GetAsync(Loop2, Languages.English, CancellationToken.None)).ShouldNotBeNull();
        copy.MomentId.ShouldBe(InLoop2(FullTime).Moment.Id);
        copy.NarrativeId.ShouldBe(original.NarrativeId.Replace(Loop1 + ":", Loop2 + ":", StringComparison.Ordinal));
        copy.Ssml.ShouldBe(original.Ssml);
        (await _recaps.GetAudioAsync(Loop2, Languages.English, CancellationToken.None)).ShouldBe(await _recaps.GetAudioAsync(Loop1, Languages.English, CancellationToken.None));
    }

    [Fact]
    public async Task A_moment_before_full_time_never_reads_the_recap_store()
    {
        await WriteLoop1Async(Goal);
        var recaps = new CountingRecapStore();
        var replays = new EarlierLoopNarratives(
            _audit, recaps, Options.Create(new AgentsOptions()), TimeProvider.System, NullLogger<EarlierLoopNarratives>.Instance, _store);

        (await replays.TryPublishAsync(InLoop2(Goal), new InMemoryNarrativeLog().PublishAsync, CancellationToken.None)).ShouldBeTrue();

        recaps.Reads.ShouldBe(0);
    }

    private const string AuditId = "01JBQ3Z8X0A1B2C3D4E5F6G7H8";

    private EarlierLoopNarratives Replays(AgentsOptions? options = null, TimeProvider? time = null) => Replays(_store, options, time);

    private EarlierLoopNarratives Replays(IMatchStore? store, AgentsOptions? options = null, TimeProvider? time = null) => new(
        _audit, _recaps, Options.Create(options ?? new AgentsOptions()), time ?? TimeProvider.System, NullLogger<EarlierLoopNarratives>.Instance, store);

    /// <summary>
    /// Runs the whole scripted crew on the moment in loop 1 and stores what it wrote (all of it, or what
    /// <paramref name="keep"/> keeps), with one model call's record.
    /// </summary>
    private async Task<IReadOnlyList<Narrative>> WriteLoop1Async(DetectedMoment moment, RecapWriter? recaps = null, Func<Narrative, bool>? keep = null)
    {
        var context = await Crew.FullWorkflow(recaps: recaps).RunAsync(moment.WorkItem, CancellationToken.None);
        foreach (var narrative in context.Narratives.Where(keep ?? (_ => true)))
        {
            await _store.SaveNarrativeAsync(narrative, CancellationToken.None);
        }

        await _audit.SaveAsync(new AuditRecord
        {
            Id = AuditId,
            MatchId = Loop1,
            MomentId = moment.Moment.Id,
            Stage = "explainer",
            Provider = "Scripted",
            Model = "scripted",
            PromptVersion = "explainer-v1",
            PromptHash = new string('0', 64),
            InputTokens = 1200,
            OutputTokens = 80,
            LatencyMs = 900,
            Outcome = AuditOutcome.Ok,
            RequestId = string.Empty,
            Utc = DateTimeOffset.UnixEpoch,
        }, CancellationToken.None);
        return context.Narratives;
    }

    private static bool IsCasualSpanish(Narrative n) => n.Persona == Personas.Casual && n.Language == Languages.Spanish;

    private static MomentWorkItem InLoop2(DetectedMoment moment) => InLoop(moment, Loop2);

    /// <summary>The moment as a later loop detects it: the same moment, sheet and document under the loop's match id.</summary>
    private static MomentWorkItem InLoop(DetectedMoment moment, string matchId) => new(
        moment.Moment with { Id = matchId + moment.Moment.Id[Loop1.Length..], MatchId = matchId },
        moment.Sheet with { MatchId = matchId },
        moment.Document with { MatchId = matchId });
}
