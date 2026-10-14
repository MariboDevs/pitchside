namespace Pitchside.Contracts;

/// <summary>
/// A span of events a rule decided matters. <see cref="Id"/> is <c>{matchId}:M:{triggerSeq:0000}:{type}</c>, so it
/// is derivable from the trigger. <see cref="Headline"/> is a code template, never model output.
/// <see cref="FactSheetSeq"/> is the sequence the agent crew snapshots the fact sheet at, so every citation
/// resolves to the values the detector saw.
/// </summary>
public sealed record Moment
{
    public required string Id { get; init; }
    public required string MatchId { get; init; }
    public required string Type { get; init; }
    public required int Priority { get; init; }
    public required int Period { get; init; }
    public required int ClockMs { get; init; }
    public required int TriggerSeq { get; init; }
    public required MomentWindow Window { get; init; }
    public string? SideId { get; init; }
    public required IReadOnlyList<string> PlayerIds { get; init; }
    public required MomentEvidence Evidence { get; init; }
    public required string Headline { get; init; }
    public required int FactSheetSeq { get; init; }

    /// <summary>The derivable moment id.</summary>
    public static string IdFor(string matchId, int triggerSeq, string type) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{matchId}:M:{triggerSeq:0000}:{type}");
}
