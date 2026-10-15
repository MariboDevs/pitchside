using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.IntegrationTests;

/// <summary>The in-memory store, except that its first narrative save fails, as a store briefly unavailable would.</summary>
internal sealed class FailOnceSaveStore(IMatchStore inner) : IMatchStore
{
    private int _saves;

    public Task SaveNarrativeAsync(Narrative narrative, CancellationToken cancellationToken) =>
        Interlocked.Increment(ref _saves) == 1
            ? throw new IOException("The store is briefly unavailable.")
            : inner.SaveNarrativeAsync(narrative, cancellationToken);

    public Task SaveMatchAsync(StoredMatch match, CancellationToken cancellationToken) => inner.SaveMatchAsync(match, cancellationToken);

    public Task<bool> TryCreateMatchAsync(StoredMatch match, CancellationToken cancellationToken) => inner.TryCreateMatchAsync(match, cancellationToken);

    public Task<StoredMatch?> GetMatchAsync(string matchId, CancellationToken cancellationToken) => inner.GetMatchAsync(matchId, cancellationToken);

    public Task<IReadOnlyList<StoredMatch>> ListMatchesAsync(CancellationToken cancellationToken) => inner.ListMatchesAsync(cancellationToken);

    public Task UpdateMatchStatusAsync(string matchId, MatchStatus status, int lastSeq, CancellationToken cancellationToken) =>
        inner.UpdateMatchStatusAsync(matchId, status, lastSeq, cancellationToken);

    public Task AppendEventAsync(MatchEvent matchEvent, CancellationToken cancellationToken) => inner.AppendEventAsync(matchEvent, cancellationToken);

    public Task<IReadOnlyList<MatchEvent>> GetEventsAsync(string matchId, int fromSeq, int toSeq, CancellationToken cancellationToken) =>
        inner.GetEventsAsync(matchId, fromSeq, toSeq, cancellationToken);

    public Task SaveFactSheetAsync(FactSheet sheet, CancellationToken cancellationToken) => inner.SaveFactSheetAsync(sheet, cancellationToken);

    public Task<FactSheet?> GetFactSheetAsync(string matchId, int atSeq, CancellationToken cancellationToken) => inner.GetFactSheetAsync(matchId, atSeq, cancellationToken);

    public Task SaveMomentAsync(Moment moment, CancellationToken cancellationToken) => inner.SaveMomentAsync(moment, cancellationToken);

    public Task<IReadOnlyList<Moment>> GetMomentsAsync(string matchId, CancellationToken cancellationToken) => inner.GetMomentsAsync(matchId, cancellationToken);

    public Task<IReadOnlyList<Narrative>> GetNarrativesAsync(string matchId, CancellationToken cancellationToken) => inner.GetNarrativesAsync(matchId, cancellationToken);

    public Task SaveOverlayEntryAsync(OverlayEntry entry, CancellationToken cancellationToken) => inner.SaveOverlayEntryAsync(entry, cancellationToken);

    public Task<IReadOnlyList<OverlayEntry>> GetOverlayEntriesAsync(string matchId, CancellationToken cancellationToken) => inner.GetOverlayEntriesAsync(matchId, cancellationToken);
}
