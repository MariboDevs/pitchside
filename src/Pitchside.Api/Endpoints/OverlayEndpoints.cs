using Microsoft.AspNetCore.Http.HttpResults;
using Pitchside.Api.Broadcast;
using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Endpoints;

/// <summary>The overlay timeline, the producer's hold, air and approval controls, and narratives by persona and language.</summary>
public static class OverlayEndpoints
{
    public static IEndpointRouteBuilder MapOverlayEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/v1/matches/{matchId}");

        group.MapGet("/overlay", async Task<Results<Ok<IReadOnlyList<OverlayEntry>>, NotFound>> (
            string matchId, int? fromMs, IMatchStore store, OverlayCoordinator overlay, CancellationToken ct) =>
        {
            if (await store.GetMatchAsync(matchId, ct).ConfigureAwait(false) is null)
            {
                return TypedResults.NotFound();
            }

            var entries = await overlay.EntriesAsync(matchId, ct).ConfigureAwait(false);
            return TypedResults.Ok<IReadOnlyList<OverlayEntry>>([.. entries.Where(e => fromMs is null || e.OutAtMs >= fromMs)]);
        }).WithName("GetOverlay");

        group.MapPost("/overlay/{entryId}/hold", async Task<Results<Ok<OverlayEntry>, NotFound>> (
            string matchId, string entryId, OverlayCoordinator overlay, CancellationToken ct) =>
            await overlay.HoldAsync(matchId, entryId, ct).ConfigureAwait(false) is { } entry ? TypedResults.Ok(entry) : TypedResults.NotFound())
            .WithName("HoldOverlayEntry");

        group.MapPost("/overlay/{entryId}/air", async Task<Results<Ok<OverlayEntry>, NotFound>> (
            string matchId, string entryId, OverlayCoordinator overlay, CancellationToken ct) =>
            await overlay.AirAsync(matchId, entryId, ct).ConfigureAwait(false) is { } entry ? TypedResults.Ok(entry) : TypedResults.NotFound())
            .WithName("AirOverlayEntry");

        group.MapPost("/approval", async Task<Results<Ok<ApprovalModeUpdate>, NotFound>> (
            string matchId, ApprovalRequest request, IMatchStore store, OverlayCoordinator overlay, CancellationToken ct) =>
        {
            if (await store.GetMatchAsync(matchId, ct).ConfigureAwait(false) is null)
            {
                return TypedResults.NotFound();
            }

            await overlay.SetModeAsync(matchId, request.Mode, ct).ConfigureAwait(false);
            return TypedResults.Ok(new ApprovalModeUpdate(matchId, request.Mode));
        }).WithName("SetApprovalMode");

        group.MapGet("/narratives", async Task<Results<Ok<IReadOnlyList<Narrative>>, NotFound>> (
            string matchId, string? persona, string? language, IMatchStore store, CancellationToken ct) =>
        {
            if (await store.GetMatchAsync(matchId, ct).ConfigureAwait(false) is null)
            {
                return TypedResults.NotFound();
            }

            var narratives = await store.GetNarrativesAsync(matchId, ct).ConfigureAwait(false);
            return TypedResults.Ok<IReadOnlyList<Narrative>>([.. narratives.Where(n =>
                (persona is null || n.Persona == persona) && (language is null || n.Language == language))]);
        }).WithName("GetNarratives");

        return app;
    }
}
