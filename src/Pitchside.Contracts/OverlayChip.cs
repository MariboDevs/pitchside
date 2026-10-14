namespace Pitchside.Contracts;

/// <summary>
/// One stat on a lower third: a label, the fact it shows, and that fact's value as displayed, rendered by code at
/// the fact's precision from the sheet the moment was detected at. The client computes nothing.
/// </summary>
public sealed record OverlayChip(string Label, string FactKey, string Display);
