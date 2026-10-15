using Microsoft.Extensions.Options;
using Pitchside.Api.Middleware;
using Pitchside.Runtime.Replay;
using Pitchside.Runtime.Usage;

namespace Pitchside.Api.Endpoints;

/// <summary><c>GET /api/v1/usage</c>: current counts against the usage guard's limits, for the caller and this host (WP07-R5).</summary>
public static class UsageEndpoints
{
    public static IEndpointRouteBuilder MapUsageEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/v1/usage", (HttpContext context, UsageGuard guard, IReplayService replays, IOptions<ReplayOptions> options) =>
            TypedResults.Ok(new UsageReport(guard.Enabled, guard.Snapshot(ClientKey.Of(context)), replays.Running.Count, options.Value.MaxConcurrent)))
            .WithName("GetUsage");
        return app;
    }
}
