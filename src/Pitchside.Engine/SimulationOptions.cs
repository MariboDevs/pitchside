namespace Pitchside.Engine;

/// <summary>
/// The engine's tunable rates. Defaults are set so that a large sample reproduces the targets in
/// data/calibration/targets.json; see docs/CALIBRATION.md and the calibration report.
/// </summary>
public sealed record SimulationOptions
{
    /// <summary>Probability that a home possession from open play ends in a shot, before rating and state modifiers.</summary>
    public double HomeShotRate { get; init; } = 0.0915;

    /// <summary>Probability that an away possession from open play ends in a shot, before rating and state modifiers.</summary>
    public double AwayShotRate { get; init; } = 0.0745;

    /// <summary>Probability that a corner possession produces a shot.</summary>
    public double CornerShotRate { get; init; } = 0.32;

    /// <summary>Probability that a non-shot possession ends with a foul by the defending team.</summary>
    public double FoulRate { get; init; } = 0.092;

    /// <summary>Probability that a non-shot, non-foul possession ends with the ball out of play.</summary>
    public double OutOfPlayRate { get; init; } = 0.30;

    /// <summary>Probability a foul draws a yellow card.</summary>
    public double YellowPerFoul { get; init; } = 0.158;

    /// <summary>Probability a foul draws a straight red card.</summary>
    public double RedPerFoul { get; init; } = 0.0040;

    /// <summary>Shot-rate multiplier for a team that is leading.</summary>
    public double LeadingFactor { get; init; } = 0.95;

    /// <summary>Shot-rate multiplier for a team that is trailing.</summary>
    public double TrailingFactor { get; init; } = 1.05;

    /// <summary>Shot-rate multiplier in the first half; the second half uses 2 minus this.</summary>
    public double FirstHalfFactor { get; init; } = 0.89;

    /// <summary>Standard deviation of the per-match log-normal tempo multiplier applied to both sides' shot rates.</summary>
    public double TempoSpread { get; init; } = 0.12;

    /// <summary>Standard deviation of each side's independent log-normal form-on-the-day multiplier.</summary>
    public double FormSpread { get; init; } = 0.22;

    /// <summary>Mean passes in a possession.</summary>
    public double MeanPasses { get; init; } = 4.2;

    public static SimulationOptions Default { get; } = new();
}
