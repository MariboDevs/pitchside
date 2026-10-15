using Pitchside.Runtime.Replay;

namespace Pitchside.Runtime.Tests;

/// <summary>A replay service for actor tests that never start one.</summary>
internal sealed class UnusedReplays : IReplayService
{
    public IReadOnlyCollection<string> Running => [];

    public bool IsInstant(string matchId) => false;

    public Task<ReplayStarted> StartAsync(long seed, double speed, CancellationToken cancellationToken) => throw new NotSupportedException();

    public bool Stop(string matchId) => false;

    public bool Pause(string matchId) => false;

    public bool Resume(string matchId) => false;
}
