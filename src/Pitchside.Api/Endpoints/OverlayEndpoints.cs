using Akka.Actor;
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

        group.MapPost("/overlay/{entryId}/hold", (string matchId, string entryId, OverlayCoordinator overlay, CancellationToken ct) =>
            ControlAsync(() => overlay.HoldAsync(matchId, entryId, ct)))
            .WithName("HoldOverlayEntry");

        group.MapPost("/overlay/{entryId}/air", (string matchId, string entryId, OverlayCoordinator overlay, CancellationToken ct) =>
            ControlAsync(() => overlay.AirAsync(matchId, entryId, ct)))
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
            string matchId, string? persona, string? language, string? subject, IMatchStore store, CancellationToken ct) =>
        {
            if (await store.GetMatchAsync(matchId, ct).ConfigureAwait(false) is null)
            {
                return TypedResults.NotFound();
            }

            var narratives = await store.GetNarrativesAsync(matchId, ct).ConfigureAwait(false);
            return TypedResults.Ok<IReadOnlyList<Narrative>>([.. narratives.Where(n =>
                (persona is null || n.Persona == persona) && (language is null || n.Language == language) && (subject is null || n.Subject == subject))]);
        }).WithName("GetNarratives");

        group.MapGet("/facts/series", GetSeriesAsync).WithName("GetFactSeries");

        return app;
    }

    /// <summary>
    /// Runs a hold or an air. Both are timed against the match actor's live clock, so while the actor is too busy to
    /// answer (deep in an instant replay) the control is refused with 503 and nothing changes, rather than a card being
    /// timed against a clock that is behind.
    /// </summary>
    private static async Task<Results<Ok<OverlayEntry>, NotFound, ProblemHttpResult>> ControlAsync(Func<Task<OverlayEntry?>> control)
    {
        try
        {
            return await control().ConfigureAwait(false) is { } entry ? TypedResults.Ok(entry) : TypedResults.NotFound();
        }
        catch (AskTimeoutException)
        {
            return TypedResults.Problem("The match is busy; try the control again.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Chosen facts sampled every <paramref name="every"/> events, recomputed from the stored events in one pass, so
    /// a client that joins late can draw the xG race and score history without computing anything itself.
    /// </summary>
    private static async Task<Results<Ok<IReadOnlyList<FactSeriesPoint>>, NotFound, ValidationProblem>> GetSeriesAsync(
        string matchId, string? keys, int? every, IMatchStore store, CancellationToken ct)
    {
        var wanted = (keys ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var step = every ?? 10;
        if (wanted.Length is 0 or > 12 || step is < 1 or > 500)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["keys"] = ["Give 1 to 12 comma-separated fact keys and a step from 1 to 500 events."],
            });
        }

        var match = await store.GetMatchAsync(matchId, ct).ConfigureAwait(false);
        if (match is null)
        {
            return TypedResults.NotFound();
        }

        var events = await store.GetEventsAsync(matchId, 1, int.MaxValue, ct).ConfigureAwait(false);
        var engine = new Pitchside.Stats.MatchStatsEngine();
        var state = engine.Start(match.Document);
        var points = new List<FactSeriesPoint>();
        for (var i = 0; i < events.Count; i++)
        {
            state = engine.Apply(state, events[i]).State;
            if ((i + 1) % step == 0 || i == events.Count - 1 || events[i].Type is EventType.Goal)
            {
                var sheet = engine.Snapshot(state);
                points.Add(new FactSeriesPoint(sheet.AsOfSeq, sheet.Period, sheet.ClockMs,
                    wanted.ToDictionary(k => k, k => sheet.Find(k)?.Value.Number ?? 0, StringComparer.Ordinal)));
            }
        }

        return TypedResults.Ok<IReadOnlyList<FactSeriesPoint>>(points);
    }
}
