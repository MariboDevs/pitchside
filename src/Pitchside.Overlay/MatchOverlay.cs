using Pitchside.Contracts;

namespace Pitchside.Overlay;

/// <summary>One match's mutable overlay state, guarded by <see cref="OverlayBook"/>'s lock on it.</summary>
public sealed class MatchOverlay
{
    public ApprovalMode Mode { get; set; } = ApprovalMode.Auto;

    public Dictionary<string, OverlayEntry> Entries { get; } = new(StringComparer.Ordinal);

    /// <summary>Each scheduled card's latest start, fixed by its moment so moving a card never extends it.</summary>
    public Dictionary<string, int> Deadlines { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether the entries stored for this match have been read back after a restart.</summary>
    public bool Loaded { get; set; }
}
