namespace Pitchside.Contracts;

/// <summary>What a moment rests on: the events that triggered it and the fact keys a narrative may cite.</summary>
public sealed record MomentEvidence(
    IReadOnlyList<string> EventIds,
    IReadOnlyList<string> FactKeys);
