namespace Pitchside.Runtime.Replay;

/// <summary>
/// <c>Demo:*</c> (WP-11): the hosted demo starts its chosen seed by itself, at <see cref="Speed"/>, so a judge who opens
/// the link finds a match already being played, and starts it again under a new match id when it ends.
/// </summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    public bool AutoStart { get; set; }

    /// <summary>The demo seed, from <c>docs/DEMO.md</c>.</summary>
    public long Seed { get; set; } = 26;

    public double Speed { get; set; } = 1.0;

    /// <summary>
    /// Replay the seed again once a replay ends, as <c>MER-26-2</c>, <c>MER-26-3</c> and so on: a match id is replayed
    /// once per store.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>How long the finished match stays the latest one before the next loop starts.</summary>
    public int LoopPauseSeconds { get; set; } = 120;

    /// <summary>The highest loop number tried, so a store full of earlier loops cannot keep the host searching.</summary>
    public int MaxLoops { get; set; } = 1_000;
}
