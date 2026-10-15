namespace Pitchside.Runtime.Usage;

/// <summary>
/// Whether a use was allowed and, if not, how long until the window resets. <see cref="Window"/> is the hour the use
/// was counted in, so a use given back with <see cref="UsageGuard.Release"/> is only ever taken off that hour.
/// </summary>
public readonly record struct UsageDecision(bool Allowed, TimeSpan RetryAfter, long Window = 0);
