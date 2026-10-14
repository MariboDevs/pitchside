namespace Pitchside.Contracts;

/// <summary>
/// Text written about a moment for one persona in one language. Every number is a <c>[[fact.key]]</c> token in
/// <see cref="Sentences"/>; <see cref="RenderedText"/> is produced by code substituting each token with the
/// fact's value at its precision. <see cref="Id"/> is <c>{momentId}:{persona}:{language}</c>.
/// </summary>
public sealed record Narrative
{
    public required string Id { get; init; }
    public required string MatchId { get; init; }
    public required string MomentId { get; init; }
    public required string Persona { get; init; }
    public required string Language { get; init; }
    public required int FactSheetSeq { get; init; }
    public required IReadOnlyList<NarrativeSentence> Sentences { get; init; }
    public required string RenderedText { get; init; }
    public required NarrativeStatus Status { get; init; }
    public required NarrativeProvenance Provenance { get; init; }
    public required NarrativeEvaluation Evaluation { get; init; }

    /// <summary>The derivable narrative id.</summary>
    public static string IdFor(string momentId, string persona, string language) => $"{momentId}:{persona}:{language}";
}
