namespace Pitchside.Runtime.Replay;

/// <summary>Replays seeded matches onto the bus as if they were live.</summary>
public interface IReplayService
{
    /// <summary>Simulates the seed, stores the document and starts publishing. Throws when the cap is reached.</summary>
    Task<ReplayStarted> StartAsync(long seed, double speed, CancellationToken cancellationToken);

    bool Stop(string matchId);

    bool Pause(string matchId);

    bool Resume(string matchId);

    /// <summary>
    /// Runs a replay ahead to ten seconds of match time before its next moment, then on at its own speed (WP-11), resuming
    /// it if it was paused. False
    /// when the match is not replaying; <paramref name="jump"/> is null when no moment is left ahead.
    /// </summary>
    bool TryJump(string matchId, out ReplayJump? jump);

    IReadOnlyCollection<string> Running { get; }

    /// <summary>
    /// Whether this process replayed the match as fast as possible (speed 0), so its clock runs far ahead of real time;
    /// still true after its events are all published, while its actor works through them.
    /// </summary>
    bool IsInstant(string matchId);
}
