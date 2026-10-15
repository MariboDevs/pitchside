namespace Pitchside.Api.Mcp;

/// <summary><c>Mcp:*</c>: the key <c>explain_moment</c> requires in the <c>X-Pitchside-Key</c> header, when set.</summary>
public sealed class McpOptions
{
    public const string SectionName = "Mcp";
    public const string KeyHeader = "X-Pitchside-Key";

    public string? ApiKey { get; set; }
}
