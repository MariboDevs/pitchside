using System.Collections.Concurrent;
using Pitchside.Contracts;

namespace Pitchside.Runtime.Audit;

/// <summary>The local audit store: one ordered list per match, in process.</summary>
public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentDictionary<string, List<AuditRecord>> _matches = new(StringComparer.Ordinal);

    public Task SaveAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var records = _matches.GetOrAdd(record.MatchId, _ => []);
        lock (records)
        {
            records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditRecord>> ListAsync(string matchId, string? after, int limit, CancellationToken cancellationToken)
    {
        if (!_matches.TryGetValue(matchId, out var records))
        {
            return Task.FromResult<IReadOnlyList<AuditRecord>>([]);
        }

        lock (records)
        {
            IReadOnlyList<AuditRecord> page = [.. records
                .Where(r => after is null || string.CompareOrdinal(r.Id, after) > 0)
                .OrderBy(r => r.Id, StringComparer.Ordinal)
                .Take(limit)];
            return Task.FromResult(page);
        }
    }
}
