using Microsoft.AspNetCore.Http.HttpResults;
using Pitchside.Contracts;
using Pitchside.Engine;

namespace Pitchside.Api;

/// <summary>
/// The first slice of the API: a seeded match on demand. The live ingest path (match actors, SignalR, Event Hubs)
/// lands on top of this; see docs/ARCHITECTURE.md.
/// </summary>
public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/v1");

        group.MapGet("/clubs", () => TypedResults.Ok(MeridianLeague.Clubs.Select(c => c.Club)))
            .WithName("ListClubs");

        group.MapGet("/matches/{seed:long}", Results<Ok<MatchDocument>, NotFound> (long seed) =>
            seed <= 0 ? TypedResults.NotFound() : TypedResults.Ok(MatchSimulator.Simulate(seed).Document))
            .WithName("GetMatch");

        group.MapGet("/matches/{seed:long}/events", Results<Ok<IReadOnlyList<MatchEvent>>, NotFound> (long seed) =>
            seed <= 0 ? TypedResults.NotFound() : TypedResults.Ok(MatchSimulator.Simulate(seed).Events))
            .WithName("GetMatchEvents");

        return app;
    }
}
