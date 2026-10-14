using Pitchside.Contracts;

namespace Pitchside.Runtime.Storage;

/// <summary>
/// Everything the runtime keeps about a match, one container per document type, partitioned by match id. The
/// match actor is the only writer of events, fact sheets and moments; the agent crew writes narratives.
/// </summary>
public interface IMatchStore
{
    Task SaveMatchAsync(StoredMatch match, CancellationToken cancellationToken);

    Task<StoredMatch?> GetMatchAsync(string matchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredMatch>> ListMatchesAsync(CancellationToken cancellationToken);

    Task UpdateMatchStatusAsync(string matchId, MatchStatus status, int lastSeq, CancellationToken cancellationToken);

    Task AppendEventAsync(MatchEvent matchEvent, CancellationToken cancellationToken);

    /// <summary>Events with <paramref name="fromSeq"/> &lt;= seq &lt;= <paramref name="toSeq"/>, in order.</summary>
    Task<IReadOnlyList<MatchEvent>> GetEventsAsync(string matchId, int fromSeq, int toSeq, CancellationToken cancellationToken);

    Task SaveFactSheetAsync(FactSheet sheet, CancellationToken cancellationToken);

    /// <summary>The stored sheet with the highest <c>asOfSeq</c> at or below <paramref name="atSeq"/>.</summary>
    Task<FactSheet?> GetFactSheetAsync(string matchId, int atSeq, CancellationToken cancellationToken);

    Task SaveMomentAsync(Moment moment, CancellationToken cancellationToken);

    Task<IReadOnlyList<Moment>> GetMomentsAsync(string matchId, CancellationToken cancellationToken);

    Task SaveNarrativeAsync(Narrative narrative, CancellationToken cancellationToken);

    Task<IReadOnlyList<Narrative>> GetNarrativesAsync(string matchId, CancellationToken cancellationToken);

    Task SaveOverlayEntryAsync(OverlayEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<OverlayEntry>> GetOverlayEntriesAsync(string matchId, CancellationToken cancellationToken);
}
