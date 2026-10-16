using Pitchside.Agents.Checking;
using Pitchside.Agents.Prompting;
using Pitchside.Agents.Rendering;
using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>
/// The scripted fallback: the moment type's code template, filled with names from the match document, verified
/// by the same checker as any model output. Templates pass by construction; a failing one is <c>rejected</c>.
/// </summary>
public sealed class FallbackComposer(PromptCatalog catalog, CodeFactChecker checker, TimeProvider time)
{
    public const string ProviderName = "template";

    public Narrative Compose(MomentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sentences = Sentences(context.Moment, context.Document, catalog);
        var verdict = checker.Check(sentences, context.FactSheet, context.Moment, context.Document);
        var provenance = new NarrativeProvenance(ProviderName, ProviderName, $"template-{context.Moment.Type}", context.Attempts, time.GetUtcNow(), true);
        return NarrativeFactory.Create(
            context, Personas.Base, Languages.English, sentences, verdict.Passed ? NarrativeStatus.Fallback : NarrativeStatus.Rejected, provenance);
    }

    /// <summary>The template's sentences for a moment: one per non-empty line, citations from the tokens.</summary>
    public static IReadOnlyList<NarrativeSentence> Sentences(Moment moment, MatchDocument document, PromptCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(catalog);
        var filled = TemplateVariables.Fill(catalog.Template(moment.Type), TemplateVariables.FromMoment(moment, document));
        return [.. filled.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith('#'))
            .Select(NarrativeRenderer.Cite)];
    }
}
