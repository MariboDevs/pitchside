using ModelContextProtocol.Client;
using ModelContextProtocol.Server;

namespace Pitchside.Mcp.Stdio;

/// <summary>Every tool of a remote MCP server as a local server tool that forwards each call, with its name, description, schema and hints.</summary>
public static class ProxyTools
{
    public static async Task<IReadOnlyList<McpServerTool>> FromAsync(McpClient remote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remote);
        var tools = await remote.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return [.. tools.Select(tool => McpServerTool.Create(tool, new McpServerToolCreateOptions
        {
            Name = tool.Name,
            Description = tool.Description,
            ReadOnly = tool.ProtocolTool.Annotations?.ReadOnlyHint,
            Idempotent = tool.ProtocolTool.Annotations?.IdempotentHint,
            Destructive = tool.ProtocolTool.Annotations?.DestructiveHint,
            OpenWorld = tool.ProtocolTool.Annotations?.OpenWorldHint,
        }))];
    }
}
