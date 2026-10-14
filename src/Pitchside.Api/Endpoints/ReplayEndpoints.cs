using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Pitchside.Runtime.Replay;

namespace Pitchside.Api.Endpoints;

/// <summary>Start, stop, pause and resume replays. Mapped only in <c>Replay</c> mode.</summary>
public static class ReplayEndpoints
{
    public static IEndpointRouteBuilder MapReplayEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/v1/replays");

        group.MapPost("", StartAsync).WithName("StartReplay");

        group.MapDelete("/{matchId}", Results<NoContent, NotFound> (string matchId, IReplayService replays) =>
            replays.Stop(matchId) ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("StopReplay");

        group.MapPost("/{matchId}/pause", Results<NoContent, NotFound> (string matchId, IReplayService replays) =>
            replays.Pause(matchId) ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("PauseReplay");

        group.MapPost("/{matchId}/resume", Results<NoContent, NotFound> (string matchId, IReplayService replays) =>
            replays.Resume(matchId) ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("ResumeReplay");

        return app;
    }

    private static async Task<Results<Accepted<ReplayStarted>, ValidationProblem, ProblemHttpResult>> StartAsync(
        ReplayRequest request, IReplayService replays, IOptions<ReplayOptions> options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var speed = request.Speed ?? options.Value.DefaultSpeed;
        if (request.Seed <= 0 || speed < 0 || double.IsNaN(speed) || double.IsInfinity(speed))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["seed"] = ["seed must be a positive whole number and speed a non-negative number."],
            });
        }

        try
        {
            var started = await replays.StartAsync(request.Seed, speed, ct).ConfigureAwait(false);
            return TypedResults.Accepted($"/api/v1/matches/{started.MatchId}", started);
        }
        catch (ReplayLimitReachedException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status429TooManyRequests, title: "Replay limit reached");
        }
        catch (ReplayConflictException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Replay conflict");
        }
    }
}
