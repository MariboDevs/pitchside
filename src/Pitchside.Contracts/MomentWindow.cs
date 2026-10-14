namespace Pitchside.Contracts;

/// <summary>The span of events a moment covers, by sequence number, inclusive.</summary>
public sealed record MomentWindow(int FromSeq, int ToSeq);
