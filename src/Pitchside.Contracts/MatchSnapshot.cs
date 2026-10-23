namespace Pitchside.Contracts;

/// <summary>
/// The reply to <c>JoinMatch</c>: everything a client that joins late needs to be complete after one message,
/// including the overlay timeline and the approval mode (WP-05), and the stored match's time scale
/// (<see cref="StoredMatch.TimeScale"/>), which the overlay's presentation clock runs at between events.
/// </summary>
public sealed record MatchSnapshot(
    StoredMatch Match,
    FactSheet? FactSheet,
    IReadOnlyList<Moment> Moments,
    IReadOnlyList<Narrative> Narratives,
    IReadOnlyList<OverlayEntry> OverlayEntries,
    ApprovalMode ApprovalMode,
    double TimeScale = 1);
