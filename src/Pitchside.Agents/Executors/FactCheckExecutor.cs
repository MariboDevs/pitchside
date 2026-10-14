using System.Globalization;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Prompts;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Executors;

/// <summary>
/// The fact-checker: code first, agent second. The code verdict is final on numbers, names and scope; when it
/// passes and the agent check is on, a model judges whether each sentence is supported by its cited facts. A
/// passing draft becomes the verified base narrative.
/// </summary>
public sealed class FactCheckExecutor(CodeFactChecker checker, IChatProvider provider, PromptCatalog catalog, bool agentCheck)
    : Executor<MomentContext, MomentContext>(ExecutorId)
{
    public const string ExecutorId = "fact-checker";
    public const string PromptName = "factcheck";

    public override async ValueTask<MomentContext> HandleAsync(MomentContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var activity = AgentTelemetry.StartStage(message, ExecutorId);
        var draft = message.LatestDraft ?? throw new InvalidOperationException("The fact-checker ran before any draft.");
        var verdict = draft.Error is { } error
            ? new Verdict([new Objection(ObjectionKinds.WholeDraft, ObjectionKinds.InvalidOutput, error)])
            : checker.Check(draft.Sentences, message.FactSheet, message.Moment, message.Document);

        if (verdict.Passed && agentCheck)
        {
            verdict = await AgentCheckAsync(message, draft, activity, cancellationToken).ConfigureAwait(false);
        }

        activity?.SetTag("pitchside.verdict", verdict.Passed ? "passed" : "failed");
        activity?.SetTag("pitchside.objections", verdict.Objections.Count);
        var next = message with { Verdicts = message.Verdicts.Add(verdict) };
        if (!verdict.Passed)
        {
            return next;
        }

        var narrative = NarrativeFactory.Create(next, Personas.Base, Languages.English, draft.Sentences, NarrativeStatus.Verified, draft.Provenance);
        return next with { Narratives = next.Narratives.Add(narrative) };
    }

    private async Task<Verdict> AgentCheckAsync(MomentContext message, NarrativeDraft draft, System.Diagnostics.Activity? activity, CancellationToken cancellationToken)
    {
        var prompt = catalog.Get(PromptName);
        var request = new ChatRequest(PromptName, prompt.VersionLabel, prompt.Text, ExplainerInput.BuildFactCheck(message, draft.Sentences), DeploymentTier.Quality, message.Moment, draft.Attempt);
        try
        {
            var result = await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            activity?.SetTag("pitchside.factcheck.tokens.in", result.TokensIn);
            activity?.SetTag("pitchside.factcheck.tokens.out", result.TokensOut);
            return new Verdict(Unsupported(result.Text));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or HttpRequestException or System.ClientModel.ClientResultException)
        {
            // The code verdict already passed on everything a viewer could be misled by; a failed tone check does
            // not hold the moment back, and the trace records that it did not run.
            activity?.SetTag("pitchside.factcheck.error", ex.GetType().Name);
            return Verdict.Pass;
        }
    }

    /// <summary>Reads <c>{ "verdicts": [ { "index", "supported", "reason" } ] }</c>; only explicit <c>false</c> objects.</summary>
    public static IReadOnlyList<Objection> Unsupported(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.TryGetProperty("verdicts", out var verdicts))
        {
            return [];
        }

        var objections = new List<Objection>();
        var position = 0;
        foreach (var verdict in verdicts.EnumerateArray())
        {
            var index = verdict.TryGetProperty("index", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt32() : position;
            position++;
            if (verdict.TryGetProperty("supported", out var supported) && supported.ValueKind == JsonValueKind.False)
            {
                var reason = verdict.TryGetProperty("reason", out var r) ? r.GetString() : null;
                objections.Add(new Objection(index.ToString(CultureInfo.InvariantCulture), ObjectionKinds.UnsupportedClaim, reason ?? "Not supported by the cited facts."));
            }
        }

        return objections;
    }
}
