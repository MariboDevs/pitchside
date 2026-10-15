using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Tests;

/// <summary>
/// An in-memory store whose event appends fail for the chosen seqs until each has failed the given number of times,
/// with an <see cref="InvalidOperationException"/> or, to stand for a store timeout, a <see cref="TaskCanceledException"/>.
/// </summary>
public sealed class FlakyAppendStore(IMatchStore inner, IReadOnlyDictionary<int, int> failuresBySeq, bool timeOut = false) : IMatchStore
{
    private readonly Dictionary<int, int> _remaining = new(failuresBySeq);
    private readonly Lock _lock = new();

    public int Failures { get; private set; }

    public Task AppendEventAsync(MatchEvent matchEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(matchEvent);
        lock (_lock)
        {
            if (_remaining.TryGetValue(matchEvent.Seq, out var left) && left > 0)
            {
                _remaining[matchEvent.Seq] = left - 1;
                Failures++;
                throw timeOut
                    ? new TaskCanceledException($"Append of seq {matchEvent.Seq} timed out.")
                    : new InvalidOperationException($"Append of seq {matchEvent.Seq} failed.");
            }
        }

        return inner.AppendEventAsync(matchEvent, cancellationToken);
    }

    public Task SaveMatchAsync(StoredMatch match, CancellationToken cancellationToken) => inner.SaveMatchAsync(match, cancellationToken);

    public Task<bool> TryCreateMatchAsync(StoredMatch match, CancellationToken cancellationToken) => inner.TryCreateMatchAsync(match, cancellationToken);

    public Task<StoredMatch?> GetMatchAsync(string matchId, CancellationToken cancellationToken) => inner.GetMatchAsync(matchId, cancellationToken);

    public Task<IReadOnlyList<StoredMatch>> ListMatchesAsync(CancellationToken cancellationToken) => inner.ListMatchesAsync(cancellationToken);

    public Task UpdateMatchStatusAsync(string matchId, MatchStatus status, int lastSeq, CancellationToken cancellationToken) =>
        inner.UpdateMatchStatusAsync(matchId, status, lastSeq, cancellationToken);

    public Task<IReadOnlyList<MatchEvent>> GetEventsAsync(string matchId, int fromSeq, int toSeq, CancellationToken cancellationToken) =>
        inner.GetEventsAsync(matchId, fromSeq, toSeq, cancellationToken);

    public Task SaveFactSheetAsync(FactSheet sheet, CancellationToken cancellationToken) => inner.SaveFactSheetAsync(sheet, cancellationToken);

    public Task<FactSheet?> GetFactSheetAsync(string matchId, int atSeq, CancellationToken cancellationToken) => inner.GetFactSheetAsync(matchId, atSeq, cancellationToken);

    public Task SaveMomentAsync(Moment moment, CancellationToken cancellationToken) => inner.SaveMomentAsync(moment, cancellationToken);

    public Task<IReadOnlyList<Moment>> GetMomentsAsync(string matchId, CancellationToken cancellationToken) => inner.GetMomentsAsync(matchId, cancellationToken);

    public Task SaveNarrativeAsync(Narrative narrative, CancellationToken cancellationToken) => inner.SaveNarrativeAsync(narrative, cancellationToken);

    public Task<IReadOnlyList<Narrative>> GetNarrativesAsync(string matchId, CancellationToken cancellationToken) => inner.GetNarrativesAsync(matchId, cancellationToken);

    public Task SaveOverlayEntryAsync(OverlayEntry entry, CancellationToken cancellationToken) => inner.SaveOverlayEntryAsync(entry, cancellationToken);

    public Task<IReadOnlyList<OverlayEntry>> GetOverlayEntriesAsync(string matchId, CancellationToken cancellationToken) => inner.GetOverlayEntriesAsync(matchId, cancellationToken);
}
