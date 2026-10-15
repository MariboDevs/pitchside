using Pitchside.Contracts;
using Pitchside.Runtime.Audit;

namespace Pitchside.Evaluation.Tests;

/// <summary>An audit store whose every write fails, as a store that is down would.</summary>
internal sealed class FailingAuditStore : IAuditStore
{
    public Task SaveAsync(AuditRecord record, CancellationToken cancellationToken) =>
        throw new IOException("The audit store is unavailable.");

    public Task<IReadOnlyList<AuditRecord>> ListAsync(string matchId, string? after, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AuditRecord>>([]);
}
