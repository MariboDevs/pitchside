namespace Pitchside.Api;

/// <summary>The <c>Pitchside</c> configuration section.</summary>
public sealed class PitchsideOptions
{
    public const string SectionName = "Pitchside";
    public const string ReplayMode = "Replay";
    public const string LiveMode = "Live";

    /// <summary><c>Replay</c> serves the replay endpoints; <c>Live</c> takes events only from the bus.</summary>
    public string Mode { get; set; } = ReplayMode;

    /// <summary>Whether the API hosts the agent crew in process (local mode) rather than a separate agent host.</summary>
    public bool RunAgentsInApi { get; set; } = true;
}
