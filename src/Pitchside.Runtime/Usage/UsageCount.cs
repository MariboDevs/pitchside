namespace Pitchside.Runtime.Usage;

/// <summary>One limit's current use: per caller or per host, per hour or concurrent.</summary>
public sealed record UsageCount(string Name, string Scope, int Used, int Limit);
