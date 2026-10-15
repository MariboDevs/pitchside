namespace Pitchside.Runtime.Usage;

/// <summary>
/// <c>UsageGuard:*</c>, the hard caps that keep a public demo safe (WP07-R5). <see cref="Enabled"/> unset means on,
/// except with the scripted provider: limits never apply to it, so tests run free.
/// </summary>
public sealed class UsageGuardOptions
{
    public const string SectionName = "UsageGuard";

    public bool? Enabled { get; set; }

    public int ReplaysPerHourPerIp { get; set; } = 6;

    /// <summary>Moment workflows per hour on one host; each writes a moment's base narrative and its variants.</summary>
    public int NarrativesPerHour { get; set; } = 400;

    public int SpokenLinesPerHour { get; set; } = 60;

    public int ConnectionsPerIp { get; set; } = 20;
}
