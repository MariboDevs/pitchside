using Akka.Actor;
using Akka.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Storage;
using Pitchside.Stats;

namespace Pitchside.Api.Endpoints;

/// <summary>
/// The match read API under <c>/api/v1</c>: the league's clubs, stored matches with their events, fact sheets and
/// moments, and a seeded match simulated on demand.
/// </summary>
public static class MatchEndpoints
{
    public const int MaxEventPage = 2_000;

    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/v1");

        group.MapGet("/clubs", () => TypedResults.Ok(MeridianLeague.Clubs.Select(c => c.Club)))
            .WithName("ListClubs");

        group.MapGet("/simulations/{seed:long}", Results<Ok<MatchDocument>, NotFound> (long seed) =>
            seed <= 0 ? TypedResults.NotFound() : TypedResults.Ok(MatchSimulator.Simulate(seed).Document))
            .WithName("GetSimulation");

        group.MapGet("/simulations/{seed:long}/events", Results<Ok<IReadOnlyList<MatchEvent>>, NotFound> (long seed) =>
            seed <= 0 ? TypedResults.NotFound() : TypedResults.Ok(MatchSimulator.Simulate(seed).Events))
            .WithName("GetSimulationEvents");

        group.MapGet("/matches", async (IMatchStore store, CancellationToken ct) =>
            TypedResults.Ok(await store.ListMatchesAsync(ct).ConfigureAwait(false)))
            .WithName("ListMatches");

        group.MapGet("/matches/{matchId}", async Task<Results<Ok<StoredMatch>, NotFound>> (string matchId, IMatchStore store, CancellationToken ct) =>
            await store.GetMatchAsync(matchId, ct).ConfigureAwait(false) is { } match ? TypedResults.Ok(match) : TypedResults.NotFound())
            .WithName("GetMatch");

        group.MapGet("/matches/{matchId}/events", GetEventsAsync).WithName("GetMatchEvents");
        group.MapGet("/matches/{matchId}/facts", GetFactsAsync).WithName("GetMatchFacts");

        group.MapGet("/matches/{matchId}/moments", async Task<Results<Ok<IReadOnlyList<Moment>>, NotFound>> (string matchId, IMatchStore store, CancellationToken ct) =>
            await store.GetMatchAsync(matchId, ct).ConfigureAwait(false) is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(await store.GetMomentsAsync(matchId, ct).ConfigureAwait(false)))
            .WithName("GetMatchMoments");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<MatchEvent>>, NotFound, ValidationProblem>> GetEventsAsync(
        string matchId, int? fromSeq, int? toSeq, IMatchStore store, CancellationToken ct)
    {
        var from = fromSeq ?? 1;
        var to = toSeq ?? from + MaxEventPage - 1;
        if (from < 1 || to < from || to - from >= MaxEventPage)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["fromSeq"] = [$"fromSeq must be at least 1, toSeq at least fromSeq, and a page at most {MaxEventPage} events."],
            });
        }

        if (await store.GetMatchAsync(matchId, ct).ConfigureAwait(false) is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await store.GetEventsAsync(matchId, from, to, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// The fact sheet as of <paramref name="atSeq"/>, recomputed exactly from the stored events by the stats engine,
    /// or the match actor's live sheet when it is omitted.
    /// </summary>
    private static async Task<Results<Ok<FactSheet>, NotFound, ValidationProblem>> GetFactsAsync(
        string matchId, int? atSeq, IMatchStore store, IRequiredActor<MatchSupervisorActor> supervisor, CancellationToken ct)
    {
        if (atSeq is < 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["atSeq"] = ["atSeq must not be negative."] });
        }

        var match = await store.GetMatchAsync(matchId, ct).ConfigureAwait(false);
        if (match is null)
        {
            return TypedResults.NotFound();
        }

        if (atSeq is null)
        {
            return TypedResults.Ok(await LiveFactSheet.GetAsync(supervisor, store, match, ct).ConfigureAwait(false));
        }

        return TypedResults.Ok(await RecomputedFacts.AtAsync(store, match, atSeq.Value, ct).ConfigureAwait(false));
    }
}
