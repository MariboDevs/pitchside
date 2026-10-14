namespace Pitchside.Runtime.Replay;

/// <summary>Replays seeded matches onto the bus as if they were live.</summary>
public interface IReplayService
{
    /// <summary>Simulates the seed, stores the document and starts publishing. Throws when the cap is reached.</summary>
    Task<ReplayStarted> StartAsync(long seed, double speed, CancellationToken cancellationToken);

    bool Stop(string matchId);

    bool Pause(string matchId);

    bool Resume(string matchId);

    IReadOnlyCollection<string> Running { get; }
}
