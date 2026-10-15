using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Pitchside.Mcp.Stdio;

// The stdio face of the Pitchside MCP server, for local clients such as Claude Code or VS Code: it connects to a
// running API's /mcp endpoint and serves the same tools over stdin and stdout. Logs go to stderr, so stdout carries
// only the protocol.
var options = StdioProxyOptions.FromEnvironment();
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = options.Endpoint,
    TransportMode = HttpTransportMode.StreamableHttp,
    AdditionalHeaders = options.Key is null ? null : new Dictionary<string, string> { ["X-Pitchside-Key"] = options.Key },
});
await using var remote = await McpClient.CreateAsync(transport);
var tools = await ProxyTools.FromAsync(remote, CancellationToken.None);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools(tools);
await builder.Build().RunAsync();
