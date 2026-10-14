namespace Pitchside.Stats;

/// <summary>One side's running totals. Unrounded; rounding happens only when a sheet is materialised.</summary>
public sealed record SideAccumulator
{
    public Metric Goals { get; init; } = Metric.Empty;
    public Metric Xg { get; init; } = Metric.Empty;
    public Metric Shots { get; init; } = Metric.Empty;
    public Metric ShotsOnTarget { get; init; } = Metric.Empty;
    public Metric BigChances { get; init; } = Metric.Empty;
    public Metric Passes { get; init; } = Metric.Empty;
    public Metric PassesCompleted { get; init; } = Metric.Empty;
    public double DifficultySum { get; init; }
    public Metric ProgressivePasses { get; init; } = Metric.Empty;
    public Metric FinalThirdEntries { get; init; } = Metric.Empty;
    public double PossessionMs { get; init; }
    public Metric Corners { get; init; } = Metric.Empty;
    public Metric Fouls { get; init; } = Metric.Empty;
    public Metric Yellows { get; init; } = Metric.Empty;
    public Metric Reds { get; init; } = Metric.Empty;
    public Metric Pressures { get; init; } = Metric.Empty;
    public Metric DistanceM { get; init; } = Metric.Empty;
}
