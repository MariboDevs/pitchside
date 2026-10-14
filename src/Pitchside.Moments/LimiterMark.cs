namespace Pitchside.Moments;

/// <summary>When and at what priority a rate-limited moment was last emitted.</summary>
public sealed record LimiterMark(int Period, int ClockMs, int Priority);
