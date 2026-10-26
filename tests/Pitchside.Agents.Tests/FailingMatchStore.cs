using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Agents.Tests;

/// <summary>A store whose every call fails, as Cosmos DB does in an outage.</summary>
internal sealed class FailingMatchStore : IMatchStore
{
    public Task SaveMatchAsync(StoredMatch match, CancellationToken cancellationToken) => throw Unavailable();

    public Task<bool> TryCreateMatchAsync(StoredMatch match, CancellationToken cancellationToken) => throw Unavailable();

    public Task<StoredMatch?> GetMatchAsync(string matchId, CancellationToken cancellationToken) => throw Unavailable();

    public Task<IReadOnlyList<StoredMatch>> ListMatchesAsync(CancellationToken cancellationToken) => throw Unavailable();

    public Task UpdateMatchStatusAsync(string matchId, MatchStatus status, int lastSeq, CancellationToken cancellationToken) => throw Unavailable();

    public Task AppendEventAsync(MatchEvent matchEvent, CancellationToken cancellationToken) => throw Unavailable();

    public Task<IReadOnlyList<MatchEvent>> GetEventsAsync(string matchId, int fromSeq, int toSeq, CancellationToken cancellationToken) => throw Unavailable();

    public Task SaveFactSheetAsync(FactSheet sheet, CancellationToken cancellationToken) => throw Unavailable();

    public Task<FactSheet?> GetFactSheetAsync(string matchId, int atSeq, CancellationToken cancellationToken) => throw Unavailable();

    public Task SaveMomentAsync(Moment moment, CancellationToken cancellationToken) => throw Unavailable();

    public Task<IReadOnlyList<Moment>> GetMomentsAsync(string matchId, CancellationToken cancellationToken) => throw Unavailable();

    public Task SaveNarrativeAsync(Narrative narrative, CancellationToken cancellationToken) => throw Unavailable();

    public Task<IReadOnlyList<Narrative>> GetNarrativesAsync(string matchId, CancellationToken cancellationToken) => throw Unavailable();

    public Task SaveOverlayEntryAsync(OverlayEntry entry, CancellationToken cancellationToken) => throw Unavailable();

    public Task<IReadOnlyList<OverlayEntry>> GetOverlayEntriesAsync(string matchId, CancellationToken cancellationToken) => throw Unavailable();

    private static InvalidOperationException Unavailable() => new("The store is unavailable.");
}
