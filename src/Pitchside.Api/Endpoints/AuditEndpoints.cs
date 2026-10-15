using Microsoft.AspNetCore.Http.HttpResults;
using Pitchside.Runtime.Audit;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Endpoints;

/// <summary>
/// <c>GET /api/v1/audit?matchId=&amp;after=&amp;limit=</c>: every model call of a match, paged in call order (WP07-R6).
/// <c>scope=server</c> reads the records that belong to no match, such as an MCP <c>list_matches</c> call; a match id
/// that was never stored still returns the records written about it, and is 404 only when there are none.
/// </summary>
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

    public const string ServerScope = "server";

    private static async Task<Results<Ok<AuditPage>, NotFound, ValidationProblem>> ListAsync(
        string? matchId, string? scope, string? after, int? limit, IMatchStore store, IAuditStore audit, CancellationToken ct)
    {
        var size = limit ?? DefaultPage;
        var server = string.Equals(scope, ServerScope, StringComparison.Ordinal);
        if ((string.IsNullOrWhiteSpace(matchId) && !server) || size is < 1 or > MaxPage || (scope is not null && !server))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["matchId"] = [$"matchId is required unless scope is {ServerScope}."],
                ["limit"] = [$"limit is between 1 and {MaxPage}."],
            });
        }

        var key = server ? string.Empty : matchId!;
        var items = await audit.ListAsync(key, after, size, ct).ConfigureAwait(false);
        // An empty page is 404 only for a match that is neither stored nor audited: past the end of a known match's
        // records it is an empty page, with or without a cursor.
        if (!server && items.Count == 0
            && await store.GetMatchAsync(key, ct).ConfigureAwait(false) is null
            && (after is null || (await audit.ListAsync(key, null, 1, ct).ConfigureAwait(false)).Count == 0))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new AuditPage(items, items.Count == size ? items[^1].Id : null));
    }
}
