namespace Pitchside.Contracts;

/// <summary>
/// The spoken full-time recap in one viewer language (WP-10): the SSML that was synthesised, the voice that read it,
/// and which narrative it was assembled from. The audio itself is served separately. When the language has no voice,
/// or its translation fell back to English, the English audio stands in and <see cref="EnglishFallback"/> is set, so
/// the viewer can say so.
/// </summary>
public sealed record Recap
{
    public required string MatchId { get; init; }

    /// <summary>The <c>full_time</c> moment the recap reads.</summary>
    public required string MomentId { get; init; }

    /// <summary>The narrative whose sentences were spoken.</summary>
    public required string NarrativeId { get; init; }

    /// <summary>The viewer language this recap is for.</summary>
    public required string Language { get; init; }

    /// <summary>The language the audio is actually in: <see cref="Language"/>, or English when it stands in.</summary>
    public required string SpokenLanguage { get; init; }

    /// <summary>The neural voice that read the SSML.</summary>
    public required string Voice { get; init; }

    public required bool EnglishFallback { get; init; }

    /// <summary>The SSML document sent to the speech service, shown in the control room.</summary>
    public required string Ssml { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}
