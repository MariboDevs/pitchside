using Microsoft.AspNetCore.Http.HttpResults;
using Pitchside.Runtime.Audit;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Endpoints;

/// <summary><c>GET /api/v1/audit?matchId=&amp;after=&amp;limit=</c>: every model call of a match, paged in call order (WP07-R6).</summary>
public static class AuditEndpoints
{
    public const int DefaultPage = 100;
    public const int MaxPage = 500;

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/v1/audit", ListAsync).WithName("ListAudit");
        return app;
    }

    private static async Task<Results<Ok<AuditPage>, NotFound, ValidationProblem>> ListAsync(
        string? matchId, string? after, int? limit, IMatchStore store, IAuditStore audit, CancellationToken ct)
    {
        var size = limit ?? DefaultPage;
        if (string.IsNullOrWhiteSpace(matchId) || size is < 1 or > MaxPage)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["matchId"] = ["matchId is required."],
                ["limit"] = [$"limit is between 1 and {MaxPage}."],
            });
        }

        if (await store.GetMatchAsync(matchId, ct).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound();
        }

        var items = await audit.ListAsync(matchId, after, size, ct).ConfigureAwait(false);
        return TypedResults.Ok(new AuditPage(items, items.Count == size ? items[^1].Id : null));
    }
}
