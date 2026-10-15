using Microsoft.AspNetCore.Http.HttpResults;
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
        return match is null || narrative is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(await NarrativeTraces.BuildAsync(store, match, narrative, ct).ConfigureAwait(false));
    }
}
