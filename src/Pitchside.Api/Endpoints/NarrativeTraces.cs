using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Endpoints;

/// <summary>Builds a narrative's traceability chain (WP07-R4), for the trace endpoint and the MCP tools (WP-08).</summary>
public static class NarrativeTraces
{
    /// <summary>Every sentence with each cited fact as of the narrative's sheet, and the events those facts derive from.</summary>
    public static async Task<NarrativeTrace> BuildAsync(IMatchStore store, StoredMatch match, Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(narrative);
        var sheet = await RecomputedFacts.AtAsync(store, match, narrative.FactSheetSeq, cancellationToken).ConfigureAwait(false);
        var sentences = narrative.Sentences
            .Select(s => new TraceSentence(s.Text, [.. s.Citations.Select(key => new TraceCitation(key, sheet.Find(key)))]))
            .ToList();
        var eventIds = sentences.SelectMany(s => s.Citations).SelectMany(c => c.Fact?.DerivedFrom ?? []).ToHashSet(StringComparer.Ordinal);
        return new NarrativeTrace(narrative.Id, narrative.FactSheetSeq, sentences, await EventsAsync(store, match.MatchId, eventIds, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The stored events with the given ids, in one range read.</summary>
    public static async Task<IReadOnlyList<MatchEvent>> EventsAsync(IMatchStore store, string matchId, IReadOnlySet<string> eventIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(eventIds);
        var seqs = eventIds.Select(SeqOf).Where(seq => seq > 0).ToList();
        return seqs.Count == 0
            ? []
            : [.. (await store.GetEventsAsync(matchId, seqs.Min(), seqs.Max(), cancellationToken).ConfigureAwait(false)).Where(e => eventIds.Contains(e.Id))];
    }

    /// <summary>The sequence number in an event id such as <c>MER-7-00123</c>; 0 when it has none.</summary>
    private static int SeqOf(string eventId) =>
        int.TryParse(eventId.AsSpan(eventId.LastIndexOf('-') + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seq) ? seq : 0;
}
