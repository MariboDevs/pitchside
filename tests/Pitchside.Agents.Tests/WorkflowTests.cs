using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Executors;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Runtime.MomentSink;

namespace Pitchside.Agents.Tests;

public sealed class WorkflowTests
{
    private const string GoodGoal = """{ "sentences": [ { "text": "Nathan Kim scores and Inyoni Wanderers lead [[match.score.away]]-[[match.score.home]]." } ] }""";
    private const string UncitedGoal = """{ "sentences": [ { "text": "Nathan Kim scores and Inyoni Wanderers lead 1-0." } ] }""";
    private const string NoObjections = """{ "verdicts": [] }""";

    private static readonly DetectedMoment Goal = Matches.Moment(7, "MER-7:M:0324:goal");

    private static string Answer(ChatRequest request, string explainer) =>
        request.PromptName == FactCheckExecutor.PromptName ? NoObjections : explainer;

    [Fact]
    public async Task Happy_path_verifies_on_the_first_attempt()
    {
        var context = await Crew.Workflow().RunAsync(Goal.WorkItem, CancellationToken.None);

        var narrative = context.BaseNarrative.ShouldNotBeNull();
        narrative.Status.ShouldBe(NarrativeStatus.Verified);
        narrative.Id.ShouldBe("MER-7:M:0324:goal:base:en");
        narrative.FactSheetSeq.ShouldBe(324);
        narrative.Provenance.Attempts.ShouldBe(1);
        narrative.Provenance.Fallback.ShouldBeFalse();
        narrative.Provenance.PromptVersion.ShouldBe("explainer-v1");
        narrative.RenderedText.ShouldBe("Nathan Kim finishes the move and Inyoni Wanderers make it 0-1. That goal comes from 0.63 expected goals built up across the match.");
        narrative.Evaluation.CitationCoverage.ShouldBe(1);
        context.Attempts.ShouldBe(1);
        context.Verdicts.ShouldHaveSingleItem().Passed.ShouldBeTrue();
    }

    [Fact]
    public async Task Retry_path_appends_the_objections_and_verifies_on_the_second_attempt()
    {
        var provider = new FakeChatProvider((request, _) => Answer(request, request.Attempt == 1 ? UncitedGoal : GoodGoal));

        var context = await Crew.Workflow(provider).RunAsync(Goal.WorkItem, CancellationToken.None);

        context.BaseNarrative!.Status.ShouldBe(NarrativeStatus.Verified);
        context.BaseNarrative.Provenance.Attempts.ShouldBe(2);
        context.BaseNarrative.RenderedText.ShouldBe("Nathan Kim scores and Inyoni Wanderers lead 1-0.");
        context.Verdicts.Select(v => v.Passed).ShouldBe([false, true]);
        var retry = provider.Requests.Where(r => r.PromptName == ExplainerExecutor.PromptName).ToList();
        retry.Count.ShouldBe(2);
        retry[0].UserMessage.ShouldNotContain("fixThese");
        retry[1].UserMessage.ShouldContain("fixThese");
        retry[1].UserMessage.ShouldContain(ObjectionKinds.UncitedNumber);
    }

    [Fact]
    public async Task Fallback_path_airs_the_template_after_two_failed_attempts()
    {
        var provider = new FakeChatProvider((request, _) => Answer(request, UncitedGoal));

        var context = await Crew.Workflow(provider).RunAsync(Goal.WorkItem, CancellationToken.None);

        var narrative = context.BaseNarrative!;
        narrative.Status.ShouldBe(NarrativeStatus.Fallback);
        narrative.Provenance.Fallback.ShouldBeTrue();
        narrative.Provenance.Provider.ShouldBe(FallbackComposer.ProviderName);
        narrative.Provenance.Attempts.ShouldBe(2);
        narrative.RenderedText.ShouldStartWith("Nathan Kim scores for Inyoni Wanderers in minute ");
        context.Attempts.ShouldBe(MomentWorkflow.MaxExplainerAttempts);
        provider.Requests.Count(r => r.PromptName == ExplainerExecutor.PromptName).ShouldBe(2);
    }

    [Fact]
    public async Task A_failing_model_call_falls_back_without_throwing()
    {
        var provider = new FakeChatProvider((_, _) => throw new HttpRequestException("the deployment is unavailable"));

        var context = await Crew.Workflow(provider).RunAsync(Goal.WorkItem, CancellationToken.None);

        context.BaseNarrative!.Status.ShouldBe(NarrativeStatus.Fallback);
        context.Verdicts.ShouldAllBe(v => v.Objections.Any(o => o.Kind == ObjectionKinds.InvalidOutput));
    }

    [Fact]
    public async Task Output_that_is_not_json_is_an_invalid_output_objection()
    {
        var provider = new FakeChatProvider((request, call) => Answer(request, call == 1 ? "Here is my answer: a great goal." : GoodGoal));

        var context = await Crew.Workflow(provider).RunAsync(Goal.WorkItem, CancellationToken.None);

        context.Verdicts[0].Objections.ShouldHaveSingleItem().Kind.ShouldBe(ObjectionKinds.InvalidOutput);
        context.BaseNarrative!.Status.ShouldBe(NarrativeStatus.Verified);
    }

    [Fact]
    public async Task An_unsupported_claim_from_the_agent_check_sends_the_draft_back()
    {
        var provider = new FakeChatProvider((request, call) => request.PromptName == FactCheckExecutor.PromptName
            ? (call == 2 ? """{ "verdicts": [ { "index": 0, "supported": false, "reason": "claims a lead the facts do not show" } ] }""" : NoObjections)
            : GoodGoal);

        var context = await Crew.Workflow(provider).RunAsync(Goal.WorkItem, CancellationToken.None);

        context.Verdicts[0].Objections.ShouldHaveSingleItem().Kind.ShouldBe(ObjectionKinds.UnsupportedClaim);
        context.BaseNarrative!.Status.ShouldBe(NarrativeStatus.Verified);
        context.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task With_the_agent_check_off_the_code_verdict_alone_decides()
    {
        var provider = new FakeChatProvider((_, _) => GoodGoal);

        var context = await Crew.Workflow(provider, agentFactCheck: false).RunAsync(Goal.WorkItem, CancellationToken.None);

        context.BaseNarrative!.Status.ShouldBe(NarrativeStatus.Verified);
        provider.Requests.ShouldNotContain(r => r.PromptName == FactCheckExecutor.PromptName);
    }

    [Fact]
    public async Task Every_seed_7_moment_ends_verified_or_fallback_with_the_scripted_provider()
    {
        var workflow = Crew.Workflow();
        foreach (var detected in Matches.Moments(7))
        {
            var context = await workflow.RunAsync(detected.WorkItem, CancellationToken.None);
            context.BaseNarrative!.Status.ShouldBeOneOf(NarrativeStatus.Verified, NarrativeStatus.Fallback);
            context.BaseNarrative.RenderedText.ShouldNotContain("[[");
        }
    }

    [Fact]
    public async Task Summaries_go_to_the_quality_deployment()
    {
        var provider = new FakeChatProvider((request, _) => Answer(request, """{ "sentences": [ { "text": "Full time: [[match.score.home]]-[[match.score.away]]." } ] }"""));
        var fullTime = Matches.Moments(7).Single(m => m.Moment.Type == "full_time");

        await Crew.Workflow(provider).RunAsync(fullTime.WorkItem, CancellationToken.None);

        provider.Requests.ShouldAllBe(r => r.Tier == DeploymentTier.Quality);
        await Crew.Workflow(provider).RunAsync(Goal.WorkItem, CancellationToken.None);
        provider.Requests.Where(r => r.Moment.Type == "goal" && r.PromptName == ExplainerExecutor.PromptName).ShouldAllBe(r => r.Tier == DeploymentTier.Fast);
    }

    [Fact]
    public async Task The_runner_caps_concurrent_workflows_and_loses_nothing()
    {
        var provider = new FakeChatProvider((request, _) => Answer(request, GoodGoal), TimeSpan.FromMilliseconds(40));
        var sink = new ChannelMomentSink();
        var log = new InMemoryNarrativeLog();
        var options = Options.Create(new AgentsOptions { MaxConcurrentMoments = 2 });
        var runner = new MomentWorkflowRunner(sink, Crew.Workflow(provider), log, options, NullLogger<MomentWorkflowRunner>.Instance);
        var goals = Enumerable.Range(0, 8).Select(i => Goal.WorkItem with { Moment = Goal.Moment with { Id = $"MER-7:M:{1000 + i}:goal" } }).ToList();

        await runner.StartAsync(CancellationToken.None);
        foreach (var item in goals)
        {
            await sink.EnqueueAsync(item, CancellationToken.None);
        }

        sink.Complete();
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));

        log.Narratives.Select(n => n.MomentId).Order(StringComparer.Ordinal).ShouldBe(goals.Select(g => g.Moment.Id).Order(StringComparer.Ordinal));
        provider.MaxInFlight.ShouldBeLessThanOrEqualTo(2);
        provider.MaxInFlight.ShouldBe(2);
    }

    [Fact]
    public async Task A_publisher_fault_is_logged_and_the_runner_carries_on()
    {
        var goals = Enumerable.Range(0, 4).Select(i => Goal.WorkItem with { Moment = Goal.Moment with { Id = $"MER-7:M:{2000 + i}:goal" } }).ToList();
        var publisher = new FailingPublisher(goals[0].Moment.Id);
        var sink = new ChannelMomentSink();
        var runner = new MomentWorkflowRunner(sink, Crew.Workflow(), publisher, Options.Create(new AgentsOptions()), NullLogger<MomentWorkflowRunner>.Instance);

        await runner.StartAsync(CancellationToken.None);
        foreach (var item in goals)
        {
            await sink.EnqueueAsync(item, CancellationToken.None);
        }

        sink.Complete();
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));

        runner.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
        publisher.Narratives.Select(n => n.MomentId).Distinct().Order(StringComparer.Ordinal).ShouldBe(goals.Skip(1).Select(g => g.Moment.Id));
    }

    [Fact]
    public async Task A_base_narrative_whose_first_publish_fails_is_still_published()
    {
        var publisher = new FailOncePublisher();
        var runner = new MomentWorkflowRunner(new ChannelMomentSink(), Crew.Workflow(), publisher, Options.Create(new AgentsOptions()), NullLogger<MomentWorkflowRunner>.Instance);

        var context = await runner.ProcessAsync(Goal.WorkItem, CancellationToken.None);

        publisher.Narratives.ShouldContain(n => n.Id == context.BaseNarrative!.Id);
    }

    [Theory]
    [InlineData(Personas.Casual, Languages.English)]
    [InlineData(Personas.Analyst, Languages.Afrikaans)]
    public async Task A_variant_whose_first_publish_fails_is_still_published_and_nothing_after_it_is_lost(string persona, string language)
    {
        var expected = await new MomentWorkflowRunner(new ChannelMomentSink(), Crew.FullWorkflow(), new InMemoryNarrativeLog(), Options.Create(new AgentsOptions()), NullLogger<MomentWorkflowRunner>.Instance)
            .ProcessAsync(Goal.WorkItem, CancellationToken.None);
        var publisher = new FailFirstMatchingPublisher(n => n.Persona == persona && n.Language == language);
        var runner = new MomentWorkflowRunner(new ChannelMomentSink(), Crew.FullWorkflow(), publisher, Options.Create(new AgentsOptions()), NullLogger<MomentWorkflowRunner>.Instance);

        var context = await runner.ProcessAsync(Goal.WorkItem, CancellationToken.None);

        // Every narrative of a run with no failures is written and published, translations of the failed variant too.
        context.Narratives.Select(n => n.Id).ShouldBe(expected.Narratives.Select(n => n.Id), ignoreOrder: true);
        publisher.Narratives.Select(n => n.Id).ShouldBe(expected.Narratives.Select(n => n.Id), ignoreOrder: true);
        context.Narratives.ShouldContain(n => n.Persona == persona && n.Language == Languages.Spanish);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void An_answer_outside_one_to_three_sentences_is_invalid(int count)
    {
        var sentences = string.Join(", ", Enumerable.Repeat("""{ "text": "Goal at minute [[match.clock.minute]]." }""", count));
        Should.Throw<FormatException>(() => ExplainerExecutor.Parse($$"""{ "sentences": [ {{sentences}} ] }"""));
    }

    [Fact]
    [Trait("Category", "Timing")]
    public async Task The_scripted_code_path_meets_the_latency_budget()
    {
        var workflow = Crew.Workflow();
        var timings = new List<double>();
        foreach (var detected in Matches.Moments(7))
        {
            var stopwatch = Stopwatch.StartNew();
            await workflow.RunAsync(detected.WorkItem, CancellationToken.None);
            timings.Add(stopwatch.Elapsed.TotalSeconds);
        }

        timings.Sort();
        timings[timings.Count / 2].ShouldBeLessThan(4);
        timings[(int)Math.Ceiling(timings.Count * 0.95) - 1].ShouldBeLessThan(10);
    }

    [Fact]
    public async Task Each_run_is_one_moment_activity_with_a_child_per_stage()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (activities)
                {
                    activities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        // The listener is process-wide and other classes run the same goal in parallel, so this run gets an id of
        // its own.
        var traced = Goal.WorkItem with { Moment = Goal.Moment with { Id = Goal.Moment.Id + ":traced" } };
        await Crew.Workflow().RunAsync(traced, CancellationToken.None);

        List<Activity> mine;
        lock (activities)
        {
            mine = [.. activities.Where(a => (string?)a.GetTagItem("pitchside.moment.id") == traced.Moment.Id)];
        }

        var root = mine.Single(a => a.OperationName == AgentTelemetry.MomentActivity);
        var stages = mine.Where(a => a.ParentSpanId == root.SpanId).Select(a => a.OperationName).ToList();
        stages.ShouldBe(["pitchside.stage.explainer", "pitchside.stage.fact-checker", "pitchside.stage.persona-writer", "pitchside.stage.translator"]);
        root.GetTagItem("pitchside.status").ShouldBe("Verified");
    }
}
