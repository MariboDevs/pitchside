using System.Collections.Concurrent;
using Pitchside.Engine;
using Pitchside.Runtime.Replay;

namespace Pitchside.Runtime.Tests;

/// <summary>
/// A replay service for <see cref="DemoAutoStart"/> tests: it records each start, refuses the loops it is told are
/// stored, and reports a replay as running for as many checks as it is told.
/// </summary>
internal sealed class ScriptedReplays(IReadOnlySet<int> storedLoops, int runningChecks) : IReplayService
{
    private readonly ConcurrentDictionary<string, int> _remaining = new(StringComparer.Ordinal);

    public ConcurrentQueue<int> Attempts { get; } = new();

    public ConcurrentQueue<string> Started { get; } = new();

    public IReadOnlyCollection<string> Running
    {
        get
        {
            foreach (var (matchId, left) in _remaining)
            {
                if (left <= 0)
                {
                    _remaining.TryRemove(matchId, out _);
                }
                else
                {
                    _remaining[matchId] = left - 1;
                }
            }

            return [.. _remaining.Keys];
        }
    }

    public bool IsInstant(string matchId) => false;

    public Task<ReplayStarted> StartAsync(long seed, double speed, int loop, CancellationToken cancellationToken)
    {
        Attempts.Enqueue(loop);
        var matchId = MatchLoops.MatchIdFor(seed, loop);
        if (storedLoops.Contains(loop))
        {
            throw new ReplayConflictException($"{matchId} is already stored.");
        }

        Started.Enqueue(matchId);
        _remaining[matchId] = runningChecks;
        return Task.FromResult(new ReplayStarted(matchId, seed, speed, 1));
    }

    public bool Stop(string matchId) => false;

    public bool Pause(string matchId) => false;

    public bool Resume(string matchId) => false;

    public bool TryJump(string matchId, out ReplayJump? jump)
    {
        jump = null;
        return false;
    }
}
