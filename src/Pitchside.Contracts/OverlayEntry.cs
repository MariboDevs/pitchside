namespace Pitchside.Contracts;

/// <summary>
/// A narrative placed on the broadcast timeline. Times are period clock milliseconds, so a renderer replaying the
/// match places entries identically. <see cref="Id"/> is <c>{momentId}:{layer}</c>.
/// </summary>
public sealed record OverlayEntry
{
    public required string Id { get; init; }
    public required string MatchId { get; init; }
    public required string MomentId { get; init; }
    public required OverlayLayer Layer { get; init; }
    public required OverlaySlot Slot { get; init; }
    public required int Priority { get; init; }
    public required int InAtMs { get; init; }
    public required int OutAtMs { get; init; }
    public required int Period { get; init; }
    public required string NarrativeId { get; init; }
    public required IReadOnlyList<OverlayChip> Chips { get; init; }
    public required OverlayState State { get; init; }

    /// <summary>Whether the card uses the full-width variant (full time).</summary>
    public bool FullWidth { get; init; }
}
