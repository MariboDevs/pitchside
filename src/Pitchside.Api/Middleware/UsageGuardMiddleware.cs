using Pitchside.Api.Hubs;
using Pitchside.Runtime.Usage;

namespace Pitchside.Api.Middleware;

/// <summary>
/// Enforces the per-caller limits at the edge (WP07-R5): replays started per hour, and hub connections open at once.
/// A refusal is 429 with a problem details body and a <c>Retry-After</c> header. The hub counts its own connections
/// in and out; this only refuses a new one, at negotiation or at a direct WebSocket connect.
/// </summary>
public sealed class UsageGuardMiddleware(RequestDelegate next, UsageGuard guard, IProblemDetailsService problems)
{
    /// <summary>How long a caller refused for open connections is asked to wait: connections close at any time.</summary>
    public static readonly TimeSpan ConnectionRetryAfter = TimeSpan.FromSeconds(30);

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = ClientKey.Of(context);
        var request = context.Request;

        // Routing accepts a trailing slash, so the limits must match with or without one.
        var path = request.Path.Value?.TrimEnd('/') ?? string.Empty;
        if (HttpMethods.IsPost(request.Method) && path.Equals("/api/v1/replays", StringComparison.OrdinalIgnoreCase))
        {
            var decision = guard.TryTake(UsageLimits.Replays, key);
            if (!decision.Allowed)
            {
                await RefuseAsync(context, decision.RetryAfter, "Replay rate limit reached", "This address has started its replays for this hour.").ConfigureAwait(false);
                return;
            }

            // The use is reserved before the replay is admitted and given back unless one actually started, so a
            // request refused as invalid, conflicting or over the concurrent cap does not spend the hourly quota.
            var started = false;
            try
            {
                await next(context).ConfigureAwait(false);
                started = context.Response.StatusCode == StatusCodes.Status202Accepted;
            }
            finally
            {
                if (!started)
                {
                    guard.Release(UsageLimits.Replays, key);
                }
            }

            return;
        }
        else if (IsNewHubConnection(request, path) && !guard.CanConnect(key))
        {
            await RefuseAsync(context, ConnectionRetryAfter, "Connection limit reached", "This address has too many live connections open.").ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsNewHubConnection(HttpRequest request, string path) =>
        request.Path.StartsWithSegments(MatchHub.Path, StringComparison.OrdinalIgnoreCase)
        && (path.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase)
            || (request.HttpContext.WebSockets.IsWebSocketRequest && !request.Query.ContainsKey("id")));

    private async Task RefuseAsync(HttpContext context, TimeSpan retryAfter, string title, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = ((long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = title, Detail = detail },
        }).ConfigureAwait(false);
    }
}
