using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Pitchside.Agents.Prompts;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Rendering;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Moments;

namespace Pitchside.Agents.Executors;

/// <summary>
/// The explainer agent: writes one to three sentences on why the moment matters, every number a <c>[[key]]</c>
/// token. On a retry the checker's objections are appended as "Fix these:". A failed call or unreadable answer
/// becomes a draft with an error, which the fact-checker turns into an objection.
/// </summary>
public sealed class ExplainerExecutor(IChatProvider provider, PromptCatalog catalog, TimeProvider time)
    : Executor<MomentContext, MomentContext>(ExecutorId)
{
    public const string ExecutorId = "explainer";
    public const string PromptName = "explainer";
    public const int MinSentences = 1;
    public const int MaxSentences = 3;

    public override async ValueTask<MomentContext> HandleAsync(MomentContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var activity = AgentTelemetry.StartStage(message, ExecutorId);
        var prompt = catalog.Get(PromptName);
        var attempt = message.Attempts + 1;
        var fixThese = attempt > 1 ? message.LatestVerdict?.Objections : null;
        var request = new ChatRequest(PromptName, prompt.VersionLabel, prompt.Text, ExplainerInput.Build(message, fixThese), TierFor(message.Moment), message.Moment, attempt);

        NarrativeDraft draft;
        try
        {
            var result = await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            activity?.SetTag("pitchside.provider", result.Provider);
            activity?.SetTag("pitchside.model", result.Model);
            activity?.SetTag("pitchside.tokens.in", result.TokensIn);
            activity?.SetTag("pitchside.tokens.out", result.TokensOut);
            draft = new NarrativeDraft(attempt, Parse(result.Text), Provenance(result.Provider, result.Model, prompt, attempt), null);
        }
        catch (Exception ex) when (ProviderFailure.IsRecoverable(ex))
        {
            activity?.SetTag("pitchside.error", ex.GetType().Name);
            draft = new NarrativeDraft(attempt, [], Provenance(provider.Name, provider.Name, prompt, attempt), ex.Message);
        }

        activity?.SetTag("pitchside.attempts", attempt);
        return message with { Drafts = message.Drafts.Add(draft) };
    }

    /// <summary>Half time and full time are summaries and go to the quality deployment.</summary>
    public static DeploymentTier TierFor(Moment moment)
    {
        ArgumentNullException.ThrowIfNull(moment);
        return moment.Type is MomentTypes.HalfTime or MomentTypes.FullTime ? DeploymentTier.Quality : DeploymentTier.Fast;
    }

    /// <summary>Reads <c>{ "sentences": [ { "text": ... } ] }</c>, tolerating a fenced block; citations come from the tokens.</summary>
    public static IReadOnlyList<NarrativeSentence> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var json = text.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var start = json.IndexOf('\n', StringComparison.Ordinal);
            var end = json.LastIndexOf("```", StringComparison.Ordinal);
            json = start >= 0 && end > start ? json[(start + 1)..end] : json;
        }

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("sentences", out var sentences) || sentences.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("The answer has no 'sentences' array.");
        }

        List<NarrativeSentence> parsed = [.. sentences.EnumerateArray()
            .Select(s => s.ValueKind == JsonValueKind.String ? s.GetString() : s.TryGetProperty("text", out var t) ? t.GetString() : null)
            .Select(t => t ?? throw new FormatException("A sentence has no text."))
            .Select(NarrativeRenderer.Cite)];
        return parsed.Count is >= MinSentences and <= MaxSentences
            ? parsed
            : throw new FormatException($"The answer has {parsed.Count} sentences; {MinSentences} to {MaxSentences} are allowed.");
    }

    private NarrativeProvenance Provenance(string providerName, string model, Prompt prompt, int attempt) =>
        new(providerName, model, prompt.VersionLabel, attempt, time.GetUtcNow(), false);
}
