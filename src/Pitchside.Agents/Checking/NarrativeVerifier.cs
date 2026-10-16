using Pitchside.Agents.Executors;
using Pitchside.Agents.Prompting;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Checking;

/// <summary>
/// Checks a persona variant or a translation exactly as the base narrative is checked: the code checker first, final
/// on numbers, names and scope; then, when it passes and the agent check is on, the model's support check. A failed
/// support call does not hold the variant back, as for the base.
/// </summary>
public sealed class NarrativeVerifier(CodeFactChecker checker, IChatProvider provider, PromptCatalog catalog, bool agentCheck)
{
    public async Task<Verdict> VerifyAsync(
        MomentContext context,
        IReadOnlyList<NarrativeSentence> sentences,
        string language,
        IReadOnlySet<string>? namedPlayerIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sentences);
        var verdict = checker.Check(sentences, context.FactSheet, context.Moment, context.Document, language, namedPlayerIds);
        if (!verdict.Passed || !agentCheck)
        {
            return verdict;
        }

        var prompt = catalog.Get(FactCheckExecutor.PromptName);
        var request = new ChatRequest(
            FactCheckExecutor.PromptName, prompt.VersionLabel, prompt.Text, ExplainerInput.BuildVariantCheck(context, sentences), DeploymentTier.Quality, context.Moment, 1);
        try
        {
            var result = await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            return new Verdict(FactCheckExecutor.Unsupported(result.Text));
        }
        catch (Exception ex) when (ProviderFailure.IsRecoverable(ex))
        {
            return Verdict.Pass;
        }
    }
}
