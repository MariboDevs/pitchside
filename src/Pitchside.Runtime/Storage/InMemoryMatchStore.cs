using System.Collections.Concurrent;
using Pitchside.Contracts;

namespace Pitchside.Runtime.Storage;

/// <summary>The local store and the test store: dictionaries keyed by document id, safe for concurrent use.</summary>
public sealed class InMemoryMatchStore : IMatchStore
{
    private readonly ConcurrentDictionary<string, StoredMatch> _matches = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, MatchEvent>> _events = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, FactSheet>> _sheets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Moment>> _moments = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Narrative>> _narratives = new(StringComparer.Ordinal);

    public Task SaveMatchAsync(StoredMatch match, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(match);
        _matches[match.MatchId] = match;
        return Task.CompletedTask;
    }

    public Task<StoredMatch?> GetMatchAsync(string matchId, CancellationToken cancellationToken) =>
        Task.FromResult(_matches.GetValueOrDefault(matchId));

    public Task<IReadOnlyList<StoredMatch>> ListMatchesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StoredMatch>>([.. _matches.Values.OrderBy(m => m.CreatedUtc).ThenBy(m => m.MatchId, StringComparer.Ordinal)]);

    public Task UpdateMatchStatusAsync(string matchId, MatchStatus status, int lastSeq, CancellationToken cancellationToken)
    {
        _matches.AddOrUpdate(
            matchId,
            _ => throw new KeyNotFoundException($"No match '{matchId}'."),
            (_, current) => current with { Status = MatchStatusTransition.Resolve(current.Status, status), LastSeq = Math.Max(current.LastSeq, lastSeq) });
        return Task.CompletedTask;
    }

    public Task AppendEventAsync(MatchEvent matchEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(matchEvent);
        _events.GetOrAdd(matchEvent.MatchId, _ => new())[matchEvent.Seq] = matchEvent;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MatchEvent>> GetEventsAsync(string matchId, int fromSeq, int toSeq, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MatchEvent>>(_events.TryGetValue(matchId, out var events)
            ? [.. events.Values.Where(e => e.Seq >= fromSeq && e.Seq <= toSeq).OrderBy(e => e.Seq)]
            : []);

    public Task SaveFactSheetAsync(FactSheet sheet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        _sheets.GetOrAdd(sheet.MatchId, _ => new())[sheet.AsOfSeq] = sheet;
        return Task.CompletedTask;
    }

    public Task<FactSheet?> GetFactSheetAsync(string matchId, int atSeq, CancellationToken cancellationToken) =>
        Task.FromResult(_sheets.TryGetValue(matchId, out var sheets)
            ? sheets.Values.Where(s => s.AsOfSeq <= atSeq).MaxBy(s => s.AsOfSeq)
            : null);

    public Task SaveMomentAsync(Moment moment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moment);
        _moments.GetOrAdd(moment.MatchId, _ => new(StringComparer.Ordinal))[moment.Id] = moment;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Moment>> GetMomentsAsync(string matchId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Moment>>(_moments.TryGetValue(matchId, out var moments)
            ? [.. moments.Values.OrderBy(m => m.TriggerSeq).ThenByDescending(m => m.Priority)]
            : []);

    public Task SaveNarrativeAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        _narratives.GetOrAdd(narrative.MatchId, _ => new(StringComparer.Ordinal))[narrative.Id] = narrative;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Narrative>> GetNarrativesAsync(string matchId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Narrative>>(_narratives.TryGetValue(matchId, out var narratives)
            ? [.. narratives.Values.OrderBy(n => n.FactSheetSeq).ThenBy(n => n.Id, StringComparer.Ordinal)]
            : []);
}
