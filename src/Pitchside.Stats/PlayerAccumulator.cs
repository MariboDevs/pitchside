using System.Collections.Immutable;

namespace Pitchside.Stats;

/// <summary>One player's running totals from the moment they appear. Minutes accrue on period clock while on the pitch.</summary>
public sealed record PlayerAccumulator
{
    public required string PlayerId { get; init; }
    public required string TeamId { get; init; }
    public Metric Passes { get; init; } = Metric.Empty;
    public Metric PassesCompleted { get; init; } = Metric.Empty;
    public Metric ProgressivePasses { get; init; } = Metric.Empty;
    public Metric Carries { get; init; } = Metric.Empty;
    public Metric DistanceM { get; init; } = Metric.Empty;
    public Metric TopSpeedKph { get; init; } = Metric.Empty;
    public Metric Shots { get; init; } = Metric.Empty;
    public Metric Xg { get; init; } = Metric.Empty;
    public Metric Goals { get; init; } = Metric.Empty;
    public Metric Assists { get; init; } = Metric.Empty;
    public Metric Pressures { get; init; } = Metric.Empty;
    public Metric Fouls { get; init; } = Metric.Empty;
    public Metric Cards { get; init; } = Metric.Empty;
    public Metric Sprints { get; init; } = Metric.Empty;
    public Metric TagEvents { get; init; } = Metric.Empty;
    public ImmutableArray<string> TagNames { get; init; } = [];

    /// <summary>Milliseconds on the pitch in completed stints.</summary>
    public int AccumulatedMs { get; init; }

    /// <summary>Period clock the current stint started at; null when the player is not on the pitch or the clock is stopped.</summary>
    public int? OnSinceMs { get; init; }

    public bool OnPitch { get; init; }

    /// <summary>Closes the current stint at <paramref name="clockMs"/>.</summary>
    public PlayerAccumulator StopClock(int clockMs) => OnSinceMs is { } since
        ? this with { AccumulatedMs = AccumulatedMs + Math.Max(0, clockMs - since), OnSinceMs = null }
        : this;

    /// <summary>Milliseconds on the pitch as of <paramref name="clockMs"/> in the current period.</summary>
    public int MillisecondsPlayed(int clockMs) => AccumulatedMs + (OnSinceMs is { } since ? Math.Max(0, clockMs - since) : 0);
}
