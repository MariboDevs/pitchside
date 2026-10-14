namespace Pitchside.Api;

/// <summary>
/// Every request carries an <c>X-Request-ID</c>: the caller's, or a new one. It is returned on the response and is
/// in the logging scope of everything the request logs.
/// </summary>
public sealed partial class RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
{
    public const string HeaderName = "X-Request-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var requestId = context.Request.Headers[HeaderName].FirstOrDefault() is { Length: > 0 and <= 128 } given
            ? given
            : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = requestId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId }))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
