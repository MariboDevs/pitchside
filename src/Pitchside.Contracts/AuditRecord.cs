namespace Pitchside.Contracts;

/// <summary>
/// One model call (WP07-R6): which stage of which moment called which model with which prompt, what it cost and how
/// long it took. The prompt text is never stored, only its SHA-256, so a record shows which prompt ran without
/// holding it.
/// </summary>
public sealed record AuditRecord
{
    /// <summary>A ULID: sortable by time, so a match's records page in call order.</summary>
    public required string Id { get; init; }
    public required string MatchId { get; init; }
    public required string MomentId { get; init; }

    /// <summary>The prompt that ran, which names the stage: <c>explainer</c>, <c>factcheck</c>, <c>persona-analyst</c>, <c>translator</c>.</summary>
    public required string Stage { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public required string PromptVersion { get; init; }

    /// <summary>Lower-case hex SHA-256 of the system prompt's text.</summary>
    public required string PromptHash { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long LatencyMs { get; init; }
    public required AuditOutcome Outcome { get; init; }

    /// <summary>The trace id of the call, which links it to the request or workflow that caused it; empty without one.</summary>
    public required string RequestId { get; init; }
    public required DateTimeOffset Utc { get; init; }
}
