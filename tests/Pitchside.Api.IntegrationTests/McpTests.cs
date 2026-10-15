using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Json.Schema;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pitchside.Agents;
using Pitchside.Agents.Workflow;
using Pitchside.Runtime.Audit;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Pitchside.Api.Endpoints;
using Pitchside.Contracts;

namespace Pitchside.Api.IntegrationTests;

public sealed class McpTests : IClassFixture<PitchsideApiFactory>, IAsyncLifetime
{
    private readonly PitchsideApiFactory _factory;

    public McpTests(PitchsideApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 });
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Accepted, HttpStatusCode.Conflict);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var entries = await (await client.GetAsync("/api/v1/matches/MER-7/overlay")).ReadAsync<List<OverlayEntry>>();
            if (entries.Count == 34)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("MER-7 was not narrated.");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_server_lists_its_nine_tools_and_only_explain_moment_writes()
    {
        await using var mcp = await ConnectAsync(_factory);

        var tools = await mcp.ListToolsAsync();

        tools.Select(t => t.Name).Order(StringComparer.Ordinal).ShouldBe(
        [
            "explain_moment", "get_events", "get_fact", "get_fact_sheet", "get_match", "get_narrative", "get_overlay_timeline", "list_matches", "list_moments",
        ]);
        tools.ShouldAllBe(t => !string.IsNullOrWhiteSpace(t.Description));
        tools.Where(t => t.ProtocolTool.Annotations!.ReadOnlyHint == true).Select(t => t.Name).ShouldNotContain("explain_moment");
        tools.Count(t => t.ProtocolTool.Annotations!.ReadOnlyHint == true).ShouldBe(8);
    }

    [Fact]
    public async Task Every_read_tool_answers_about_a_replayed_match_in_the_contract_shapes()
    {
        await using var mcp = await ConnectAsync(_factory);

        var matches = await CallAsync(mcp, "list_matches", []);
        matches.EnumerateArray().ShouldContain(m => m.GetProperty("matchId").GetString() == "MER-7" && m.GetProperty("status").GetString() == "completed");

        (await CallAsync(mcp, "get_match", new() { ["matchId"] = "MER-7" })).GetProperty("matchId").GetString().ShouldBe("MER-7");

        Validate("fact-sheet.schema.json", await CallAsync(mcp, "get_fact_sheet", new() { ["matchId"] = "MER-7" }));
        var atSeq = await CallAsync(mcp, "get_fact_sheet", new() { ["matchId"] = "MER-7", ["atSeq"] = 500 });
        atSeq.GetProperty("asOfSeq").GetInt32().ShouldBe(500);
        Validate("fact-sheet.schema.json", atSeq);

        var fact = await CallAsync(mcp, "get_fact", new() { ["matchId"] = "MER-7", ["key"] = "match.score.away" });
        fact.GetProperty("fact").GetProperty("key").GetString().ShouldBe("match.score.away");
        fact.GetProperty("events").EnumerateArray().ShouldAllBe(e => e.GetProperty("type").GetString() == "goal");

        var goals = await CallAsync(mcp, "list_moments", new() { ["matchId"] = "MER-7", ["type"] = "goal" });
        goals.GetArrayLength().ShouldBe(4);
        foreach (var goal in goals.EnumerateArray())
        {
            Validate("moment.schema.json", goal);
        }

        var narrative = await CallAsync(mcp, "get_narrative", new() { ["matchId"] = "MER-7", ["momentId"] = "MER-7:M:0324:goal", ["persona"] = "analyst", ["language"] = "af" });
        Validate("narrative.schema.json", narrative.GetProperty("narrative"));
        narrative.GetProperty("trace").GetProperty("sentences").GetArrayLength().ShouldBeGreaterThan(0);

        // The goal has a player-focus narrative per involved player: subject picks one, and leaving it out is an error.
        var focus = await CallAsync(mcp, "get_narrative", new() { ["matchId"] = "MER-7", ["momentId"] = "MER-7:M:0324:goal", ["persona"] = "player_focus", ["subject"] = "INY-07" });
        focus.GetProperty("narrative").GetProperty("subject").GetString().ShouldBe("INY-07");
        var ambiguous = await mcp.CallToolAsync("get_narrative", new Dictionary<string, object?> { ["matchId"] = "MER-7", ["momentId"] = "MER-7:M:0324:goal", ["persona"] = "player_focus" });
        ambiguous.IsError.ShouldBe(true);
        ambiguous.Content.OfType<TextContentBlock>().ShouldContain(c => c.Text.Contains("subject", StringComparison.Ordinal));

        var timeline = await CallAsync(mcp, "get_overlay_timeline", new() { ["matchId"] = "MER-7" });
        timeline.GetArrayLength().ShouldBe(34);
        Validate("overlay-entry.schema.json", timeline[0]);
        (await CallAsync(mcp, "get_overlay_timeline", new() { ["matchId"] = "MER-7", ["fromMs"] = 2_700_000 })).GetArrayLength().ShouldBeLessThan(34);

        var events = await CallAsync(mcp, "get_events", new() { ["matchId"] = "MER-7", ["fromSeq"] = 1, ["toSeq"] = 200 });
        events.GetArrayLength().ShouldBe(200);
        Validate("event.schema.json", events[0]);
    }

    [Fact]
    public async Task The_stdio_host_proxies_every_tool_with_its_hints()
    {
        await using var mcp = await ConnectAsync(_factory);

        var proxied = await Pitchside.Mcp.Stdio.ProxyTools.FromAsync(mcp, CancellationToken.None);

        proxied.Select(t => t.ProtocolTool.Name).Order(StringComparer.Ordinal).ShouldBe((await mcp.ListToolsAsync()).Select(t => t.Name).Order(StringComparer.Ordinal));
        proxied.Single(t => t.ProtocolTool.Name == "explain_moment").ProtocolTool.Annotations!.ReadOnlyHint.ShouldBe(false);
        proxied.Single(t => t.ProtocolTool.Name == "get_events").ProtocolTool.Annotations!.ReadOnlyHint.ShouldBe(true);
        proxied.Single(t => t.ProtocolTool.Name == "get_events").ProtocolTool.InputSchema.GetRawText().ShouldContain("fromSeq");
    }

    [Fact]
    public async Task Bad_arguments_are_tool_errors_not_crashes()
    {
        await using var mcp = await ConnectAsync(_factory);

        (await mcp.CallToolAsync("get_events", new Dictionary<string, object?> { ["matchId"] = "MER-7", ["fromSeq"] = 1, ["toSeq"] = 500 })).IsError.ShouldBe(true);
        (await mcp.CallToolAsync("get_match", new Dictionary<string, object?> { ["matchId"] = "MER-404" })).IsError.ShouldBe(true);
        (await mcp.CallToolAsync("get_fact", new Dictionary<string, object?> { ["matchId"] = "MER-7", ["key"] = "nope" })).IsError.ShouldBe(true);
    }

    [Fact]
    public async Task Explain_moment_needs_the_key_when_one_is_set_and_every_call_is_audited()
    {
        await using var keyed = new ConfiguredApiFactory(new Dictionary<string, string?> { ["Mcp:ApiKey"] = "test-key-not-a-secret" });
        var http = keyed.CreateClient();
        (await http.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && (await (await http.GetAsync("/api/v1/matches/MER-7/moments")).ReadAsync<List<Moment>>()).Count < 34)
        {
            await Task.Delay(200);
        }

        var arguments = new Dictionary<string, object?> { ["matchId"] = "MER-7", ["momentId"] = "MER-7:M:0324:goal" };
        await using (var anonymous = await ConnectAsync(keyed))
        {
            (await anonymous.CallToolAsync("explain_moment", arguments)).IsError.ShouldBe(true);
        }

        await using var withKey = await ConnectAsync(keyed, "test-key-not-a-secret");
        var explained = await withKey.CallToolAsync("explain_moment", arguments);
        explained.IsError.ShouldNotBe(true);
        var narrative = Parse(explained);
        Validate("narrative.schema.json", narrative);
        narrative.GetProperty("status").GetString().ShouldBe("verified");

        var records = new List<AuditRecord>();
        string? after = null;
        do
        {
            var page = await (await http.GetAsync("/api/v1/audit?matchId=MER-7&limit=500" + (after is null ? string.Empty : $"&after={after}"))).ReadAsync<AuditPage>();
            records.AddRange(page.Items);
            after = page.Next;
        }
        while (after is not null);

        var calls = records.Where(r => r.Stage == "mcp:explain_moment").ToList();
        calls.Count.ShouldBe(2);
        calls.Select(r => r.Outcome).ShouldBe([AuditOutcome.Error, AuditOutcome.Ok]);
        calls.ShouldAllBe(r => r.MomentId == "MER-7:M:0324:goal" && r.Provider == "mcp");
    }

    [Fact]
    public async Task Explain_moment_waits_its_turn_under_the_hosts_workflow_cap()
    {
        var gate = _factory.Services.GetRequiredService<WorkflowGate>();
        var slots = _factory.Services.GetRequiredService<IOptions<AgentsOptions>>().Value.MaxConcurrentMoments;
        for (var i = 0; i < slots; i++)
        {
            await gate.WaitAsync(CancellationToken.None);
        }

        await using var mcp = await ConnectAsync(_factory);
        var call = mcp.CallToolAsync("explain_moment", new Dictionary<string, object?> { ["matchId"] = "MER-7", ["momentId"] = "MER-7:M:0324:goal" }).AsTask();
        await Task.Delay(500);
        call.IsCompleted.ShouldBeFalse();

        for (var i = 0; i < slots; i++)
        {
            gate.Release();
        }

        (await call).IsError.ShouldNotBe(true);
    }

    [Fact]
    public async Task A_tool_call_keeps_its_result_when_the_audit_write_fails()
    {
        await using var failing = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IAuditStore, FailingAuditStore>()));
        await using var mcp = await ConnectAsync(failing);

        var result = await mcp.CallToolAsync("list_matches", new Dictionary<string, object?>());

        result.IsError.ShouldNotBe(true);
    }

    private static async Task<McpClient> ConnectAsync(WebApplicationFactory<Program> factory, string? key = null)
    {
        var options = new HttpClientTransportOptions
        {
            Endpoint = new Uri(factory.Server.BaseAddress, "mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = key is null ? null : new Dictionary<string, string> { ["X-Pitchside-Key"] = key },
        };
        return await McpClient.CreateAsync(new HttpClientTransport(options, factory.CreateClient(), NullLoggerFactory.Instance, ownsHttpClient: true));
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.IsError.ShouldNotBe(true, string.Join(' ', result.Content.OfType<TextContentBlock>().Select(c => c.Text)));
        return Parse(result);
    }

    private static JsonElement Parse(CallToolResult result) =>
        JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text).RootElement.Clone();

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, JsonSchema> Schemas = new(StringComparer.Ordinal);

    private static void Validate(string schemaFile, JsonElement value)
    {
        var schema = Schemas.GetOrAdd(schemaFile, file => JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "schemas", file))));
        var result = schema.Evaluate(value, new EvaluationOptions { OutputFormat = OutputFormat.List });
        result.IsValid.ShouldBeTrue($"{schemaFile}: {value.GetRawText()[..Math.Min(400, value.GetRawText().Length)]}");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pitchside.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Pitchside.slnx not found above the test output.");
    }
}
