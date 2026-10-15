namespace Pitchside.Api.Middleware;

/// <summary>The caller a per-caller limit counts against: the client IP, after forwarded headers are applied.</summary>
public static class ClientKey
{
    public const string Unknown = "unknown";

    public static string Of(HttpContext? context) => context?.Connection.RemoteIpAddress?.ToString() ?? Unknown;
}
