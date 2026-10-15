namespace Pitchside.Runtime.Usage;

/// <summary>Whether a use was allowed and, if not, how long until the window resets.</summary>
public readonly record struct UsageDecision(bool Allowed, TimeSpan RetryAfter);
