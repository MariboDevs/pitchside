namespace Pitchside.Contracts;

/// <summary>The span of events a rolling metric was computed over, by sequence number, inclusive.</summary>
public sealed record FactWindow(int FromSeq, int ToSeq);
