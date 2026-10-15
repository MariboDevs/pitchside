namespace Pitchside.Contracts;

/// <summary>
/// Text written about a moment for one persona in one language. Every number is a <c>[[fact.key]]</c> token in
/// <see cref="Sentences"/>; <see cref="RenderedText"/> is produced by code substituting each token with the
/// fact's value at its precision. <see cref="Id"/> is <c>{momentId}:{persona}:{language}</c>, or
/// <c>{momentId}:{persona}.{subject}:{language}</c> for a variant written for one club or one player.
/// </summary>
public sealed record Narrative
{
    public required string Id { get; init; }
    public required string MatchId { get; init; }
    public required string MomentId { get; init; }
    public required string Persona { get; init; }

    /// <summary>The club id a <c>club_fan</c> variant is written for, or the player id of a <c>player_focus</c> one.</summary>
    public string? Subject { get; init; }
    public required string Language { get; init; }
    public required int FactSheetSeq { get; init; }
    public required IReadOnlyList<NarrativeSentence> Sentences { get; init; }
    public required string RenderedText { get; init; }
    public required NarrativeStatus Status { get; init; }
    public required NarrativeProvenance Provenance { get; init; }
    public required NarrativeEvaluation Evaluation { get; init; }

    /// <summary>The derivable narrative id.</summary>
    public static string IdFor(string momentId, string persona, string language, string? subject = null) =>
        subject is null ? $"{momentId}:{persona}:{language}" : $"{momentId}:{persona}.{subject}:{language}";
}
