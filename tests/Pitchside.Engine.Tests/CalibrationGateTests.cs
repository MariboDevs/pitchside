using Pitchside.Engine;
using Pitchside.Engine.Calibration;

namespace Pitchside.Engine.Tests;

/// <summary>
/// The build fails when the engine drifts away from real-league aggregates. The sample is small enough for CI,
/// so tolerances here are the report's tolerances widened for sampling noise; the full report at 5,000 matches
/// is the published evidence.
/// </summary>
public sealed class CalibrationGateTests
{
    private const int SampleSize = 600;

    [Fact]
    public void Synthetic_aggregates_track_the_real_league_targets()
    {
        var matches = Enumerable.Range(1, SampleSize).Select(seed => MatchSimulator.Simulate(seed)).ToList();
        var report = CalibrationReport.Compare(AggregateStats.From(matches), CalibrationTargets.LoadEmbedded());

        var failures = report.Rows
            .Where(r => Math.Abs(r.Gap) > r.Tolerance * 1.5)
            .Select(r => $"{r.Statistic}: synthetic {r.Synthetic:0.000} vs target {r.Target:0.000} (tolerance {r.Tolerance:0.000})")
            .ToList();

        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));
    }
}
