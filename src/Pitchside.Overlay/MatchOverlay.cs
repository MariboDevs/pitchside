using Pitchside.Contracts;

namespace Pitchside.Overlay;

/// <summary>One match's mutable overlay state, guarded by <see cref="OverlayBook"/>'s lock on it.</summary>
public sealed class MatchOverlay
{
    public ApprovalMode Mode { get; set; } = ApprovalMode.Auto;

    public Dictionary<string, OverlayEntry> Entries { get; } = new(StringComparer.Ordinal);
}
