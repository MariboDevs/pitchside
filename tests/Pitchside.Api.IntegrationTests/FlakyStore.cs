using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.IntegrationTests;

/// <summary>
/// A store that is briefly unavailable: its first narrative save, or its first match listing, fails once, as chosen;
/// everything else goes to the store it wraps.
/// </summary>
internal sealed class FlakyStore(IMatchStore inner, bool failFirstSave = false, bool failFirstList = false) : IMatchStore
{
    private int _saves;
    private int _lists;

    public Task SaveNarrativeAsync(Narrative narrative, CancellationToken cancellationToken) =>
        failFirstSave && Interlocked.Increment(ref _saves) == 1
            ? throw new IOException("The store is briefly unavailable.")
            : inner.SaveNarrativeAsync(narrative, cancellationToken);

    public Task<IReadOnlyList<StoredMatch>> ListMatchesAsync(CancellationToken cancellationToken) =>
        failFirstList && Interlocked.Increment(ref _lists) == 1
            ? throw new IOException("The store is briefly unavailable.")
            : inner.ListMatchesAsync(cancellationToken);

    public Task SaveMatchAsync(StoredMatch match, CancellationToken cancellationToken) => inner.SaveMatchAsync(match, cancellationToken);

    public Task<bool> TryCreateMatchAsync(StoredMatch match, CancellationToken cancellationToken) => inner.TryCreateMatchAsync(match, cancellationToken);

    public Task<StoredMatch?> GetMatchAsync(string matchId, CancellationToken cancellationToken) => inner.GetMatchAsync(matchId, cancellationToken);

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
