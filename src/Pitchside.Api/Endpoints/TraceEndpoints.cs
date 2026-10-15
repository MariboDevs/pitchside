using Microsoft.AspNetCore.Http.HttpResults;
using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Endpoints;

/// <summary><c>GET /api/v1/matches/{matchId}/narratives/{narrativeId}/trace</c>: from each sentence to the events behind it (WP07-R4).</summary>
public static class TraceEndpoints
{
    public static IEndpointRouteBuilder MapTraceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/v1/matches/{matchId}/narratives/{narrativeId}/trace", GetTraceAsync).WithName("GetNarrativeTrace");
        return app;
    }

    private static async Task<Results<Ok<NarrativeTrace>, NotFound>> GetTraceAsync(string matchId, string narrativeId, IMatchStore store, CancellationToken ct)
    {
        var match = await store.GetMatchAsync(matchId, ct).ConfigureAwait(false);
        var narrative = match is null
            ? null
            : (await store.GetNarrativesAsync(matchId, ct).ConfigureAwait(false)).FirstOrDefault(n => n.Id == narrativeId);
        if (match is null || narrative is null)
        {
            return TypedResults.NotFound();
        }

        var sheet = await RecomputedFacts.AtAsync(store, match, narrative.FactSheetSeq, ct).ConfigureAwait(false);
        var sentences = narrative.Sentences
            .Select(s => new TraceSentence(s.Text, [.. s.Citations.Select(key => new TraceCitation(key, sheet.Find(key)))]))
            .ToList();
        var eventIds = sentences.SelectMany(s => s.Citations).SelectMany(c => c.Fact?.DerivedFrom ?? []).ToHashSet(StringComparer.Ordinal);
        var seqs = eventIds.Select(SeqOf).Where(seq => seq > 0).ToList();
        IReadOnlyList<MatchEvent> events = seqs.Count == 0
            ? []
            : [.. (await store.GetEventsAsync(matchId, seqs.Min(), seqs.Max(), ct).ConfigureAwait(false)).Where(e => eventIds.Contains(e.Id))];
        return TypedResults.Ok(new NarrativeTrace(narrative.Id, narrative.FactSheetSeq, sentences, events));
    }

    /// <summary>The sequence number in an event id such as <c>MER-7-00123</c>; 0 when it has none.</summary>
    private static int SeqOf(string eventId) =>
        int.TryParse(eventId.AsSpan(eventId.LastIndexOf('-') + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seq) ? seq : 0;
}
