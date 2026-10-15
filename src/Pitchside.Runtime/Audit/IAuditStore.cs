using Pitchside.Contracts;

namespace Pitchside.Runtime.Audit;

/// <summary>Where every model call's audit record is kept (WP07-R6), partitioned by match.</summary>
public interface IAuditStore
{
    Task SaveAsync(AuditRecord record, CancellationToken cancellationToken);

    /// <summary>A match's records in id (call) order, after the id <paramref name="after"/> if given, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<AuditRecord>> ListAsync(string matchId, string? after, int limit, CancellationToken cancellationToken);
}
