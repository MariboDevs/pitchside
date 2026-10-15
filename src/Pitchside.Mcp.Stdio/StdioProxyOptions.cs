namespace Pitchside.Mcp.Stdio;

/// <summary>
/// Where the proxied tools live: <c>PITCHSIDE_MCP_URL</c> (default <c>http://localhost:5180/mcp</c>) and, for
/// <c>explain_moment</c> on a server that sets <c>Mcp:ApiKey</c>, <c>PITCHSIDE_MCP_KEY</c>.
/// </summary>
public sealed record StdioProxyOptions(Uri Endpoint, string? Key)
{
    public static readonly Uri DefaultEndpoint = new("http://localhost:5180/mcp");

    public static StdioProxyOptions FromEnvironment() => new(
        Environment.GetEnvironmentVariable("PITCHSIDE_MCP_URL") is { Length: > 0 } url ? new Uri(url) : DefaultEndpoint,
        Environment.GetEnvironmentVariable("PITCHSIDE_MCP_KEY") is { Length: > 0 } key ? key : null);
}
