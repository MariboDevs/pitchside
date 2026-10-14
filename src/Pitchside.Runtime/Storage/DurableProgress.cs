using System.Collections.Concurrent;

namespace Pitchside.Runtime.Storage;

/// <summary>
/// The highest event seq per match that is in the store, written by each match's outbox after an append succeeds.
/// A bus checkpoints a delivered event only once it is durable, so a restart never skips an event the store lacks.
/// </summary>
public sealed class DurableProgress
{
    private readonly ConcurrentDictionary<string, int> _durable = new(StringComparer.Ordinal);

    public int For(string matchId) => _durable.GetValueOrDefault(matchId);

    public void Advance(string matchId, int seq) => _durable.AddOrUpdate(matchId, seq, (_, current) => Math.Max(current, seq));
}
