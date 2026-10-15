using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Pitchside.Contracts;
using Pitchside.Runtime.Audit;

namespace Pitchside.Api.Mcp;

/// <summary>
/// The MCP server at <c>/mcp</c> (WP-08): streamable HTTP, stateless, so any client can call tools without a session.
/// Two request filters wrap every tool call, apart from the tool code: the key check for <c>explain_moment</c>, the
/// place an OAuth resource-server check would also go, and an <see cref="AuditRecord"/> with stage <c>mcp:{tool}</c>.
/// </summary>
public static partial class McpServerSetup
{
    public const string Path = "/mcp";
    public const string ExplainMoment = "explain_moment";

    /// <summary>The contract's serializer settings, with the reflection resolver the SDK needs before it freezes them.</summary>
    private static readonly JsonSerializerOptions ToolJson = new(ContractJson.Options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    public static IServiceCollection AddPitchsideMcp(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<McpOptions>(configuration.GetSection(McpOptions.SectionName));
        services.AddHttpContextAccessor();
        services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<PitchsideTools>(ToolJson)
            .WithRequestFilters(filters => filters
                .AddCallToolFilter(next => async (context, cancellationToken) =>
                {
                    var started = Stopwatch.GetTimestamp();
                    CallToolResult? result = null;
                    try
                    {
                        result = Authorised(context.Services, context.Params?.Name)
                            ?? await next(context, cancellationToken).ConfigureAwait(false);
                        return result;
                    }
                    finally
                    {
                        await AuditAsync(context.Services, context.Params, result, Stopwatch.GetElapsedTime(started)).ConfigureAwait(false);
                    }
                }));
        return services;
    }

    /// <summary>A refusal when the tool needs the key and the request does not carry it; null when the call may go on.</summary>
    private static CallToolResult? Authorised(IServiceProvider? services, string? tool)
    {
        var key = services?.GetService<IOptions<McpOptions>>()?.Value.ApiKey;
        if (tool != ExplainMoment || string.IsNullOrEmpty(key))
        {
            return null;
        }

        var given = services?.GetService<IHttpContextAccessor>()?.HttpContext?.Request.Headers[McpOptions.KeyHeader].ToString() ?? string.Empty;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(key))
            ? null
            : new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = $"{ExplainMoment} needs the {McpOptions.KeyHeader} header." }] };
    }

    private static async Task AuditAsync(IServiceProvider? services, CallToolRequestParams? call, CallToolResult? result, TimeSpan elapsed)
    {
        if (services?.GetService<IAuditStore>() is not { } audit || call is null)
        {
            return;
        }

        var time = services.GetService<TimeProvider>() ?? TimeProvider.System;
        var now = time.GetUtcNow();
        var stage = $"mcp:{call.Name}";
        await audit.SaveAsync(new AuditRecord
        {
            Id = Ulid.New(now),
            MatchId = Argument(call, "matchId", MatchId()),
            MomentId = Argument(call, "momentId", MomentId()),
            Stage = ToolName().IsMatch(call.Name) ? stage : "mcp:unknown",
            Provider = "mcp",
            Model = string.Empty,
            PromptVersion = stage,
            PromptHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(call.Name))),
            InputTokens = 0,
            OutputTokens = 0,
            LatencyMs = (long)elapsed.TotalMilliseconds,
            Outcome = result is { IsError: not true } ? AuditOutcome.Ok : AuditOutcome.Error,
            RequestId = services.GetService<IHttpContextAccessor>()?.HttpContext?.TraceIdentifier ?? string.Empty,
            Utc = now,
        }, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>A string argument when it is well formed for the audit record; empty otherwise.</summary>
    private static string Argument(CallToolRequestParams call, string name, Regex valid) =>
        call.Arguments is { } arguments && arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            && value.GetString() is { } text && valid.IsMatch(text)
            ? text
            : string.Empty;

    [GeneratedRegex(@"^MER-[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex MatchId();

    [GeneratedRegex(@"^MER-[0-9]+:M:[0-9]{4,}:[a-z_]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex MomentId();

    [GeneratedRegex(@"^[a-z]+(_[a-z]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ToolName();
}
