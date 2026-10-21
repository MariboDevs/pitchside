using Pitchside.Moments.Rules;

namespace Pitchside.Fabric.Tests;

/// <summary>The Activator's pressure alert counts shots over the same window as the sustained_pressure moment.</summary>
public sealed class PressureAlertParityTests
{
    private static readonly IReadOnlyDictionary<string, double> Kql = KqlFile.Constants("fabric/activator/pressure-alerts.kql");

    [Fact]
    public void The_window_is_the_sustained_pressure_window() => Kql["windowMs"].ShouldBe(SustainedPressureRule.WindowMs);

    [Fact]
    public void The_alert_fires_at_four_shots() => Kql["shotThreshold"].ShouldBe(4);
}
