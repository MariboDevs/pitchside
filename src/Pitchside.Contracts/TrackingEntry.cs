namespace Pitchside.Contracts;

/// <summary>One player's movement over the last tracking interval: the delta, never the cumulative.</summary>
public sealed record TrackingEntry(
    string PlayerId,
    double DistanceM,
    double TopSpeedKph);
