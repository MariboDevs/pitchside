using Pitchside.Agents.Rendering;
using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>Builds a narrative for a moment: derivable id, rendered text and citation coverage from code.</summary>
public static class NarrativeFactory
{
    public static Narrative Create(
        MomentContext context, string persona, string language, IReadOnlyList<NarrativeSentence> sentences, NarrativeStatus status, NarrativeProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sentences);
        var renderable = status is NarrativeStatus.Verified or NarrativeStatus.Fallback;
        return new Narrative
        {
            Id = Narrative.IdFor(context.Moment.Id, persona, language),
            MatchId = context.Moment.MatchId,
            MomentId = context.Moment.Id,
            Persona = persona,
            Language = language,
            FactSheetSeq = context.FactSheet.AsOfSeq,
            Sentences = sentences,
            RenderedText = renderable ? NarrativeRenderer.Render(sentences, context.FactSheet) : string.Empty,
            Status = status,
            Provenance = provenance,
            Evaluation = new NarrativeEvaluation(NarrativeRenderer.CitationCoverage(sentences), null),
        };
    }
}
