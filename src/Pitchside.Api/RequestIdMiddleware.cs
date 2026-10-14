using System.Text.RegularExpressions;

namespace Pitchside.Api;

/// <summary>
/// Every request carries an <c>X-Request-ID</c>: the caller's when it is a plain token, or a new one. It is returned on
/// the response and is in the logging scope of everything the request logs. A caller's value with any other
/// character, a line break above all, is replaced rather than logged, so a header cannot forge log entries.
/// </summary>
public sealed partial class RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
{
    public const string HeaderName = "X-Request-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var requestId = context.Request.Headers[HeaderName].FirstOrDefault() is { } given && SafeId().IsMatch(given)
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

    [GeneratedRegex(@"^[A-Za-z0-9._:\-]{1,128}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SafeId();
}
