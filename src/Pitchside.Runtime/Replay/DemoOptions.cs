namespace Pitchside.Runtime.Replay;

/// <summary>
/// <c>Demo:*</c> (WP-11): the hosted demo starts its chosen seed by itself, at real-time speed, so a judge who opens
/// the link finds a match already being played.
/// </summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    public bool AutoStart { get; set; }

    /// <summary>The demo seed, from <c>docs/DEMO.md</c>.</summary>
    public long Seed { get; set; } = 26;

    public double Speed { get; set; } = 1.0;
}
