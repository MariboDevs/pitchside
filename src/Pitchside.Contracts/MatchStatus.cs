namespace Pitchside.Contracts;

/// <summary>Where a stored match is: waiting for events, being played, paused, finished, or stopped early.</summary>
public enum MatchStatus
{
    Scheduled,
    Live,
    Paused,
    Completed,
    Stopped,
}
