namespace Pitchside.Runtime.Replay;

/// <summary>The <c>Replay</c> configuration section.</summary>
public sealed class ReplayOptions
{
    public const string SectionName = "Replay";

    /// <summary>At most this many replays run at once per host: the first usage guard.</summary>
    public int MaxConcurrent { get; set; } = 3;

    /// <summary>Speed over real time when a request does not give one; 0 means as fast as possible.</summary>
    public double DefaultSpeed { get; set; } = 1.0;
}
