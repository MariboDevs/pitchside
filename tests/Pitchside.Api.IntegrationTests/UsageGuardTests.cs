using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Pitchside.Api.Endpoints;
using Pitchside.Contracts;
using Pitchside.Runtime.Usage;

namespace Pitchside.Api.IntegrationTests;

public sealed class UsageGuardTests
{
    /// <summary>
    /// Waits until the guard counts exactly this many open connections: the handshake can complete before the hub takes
    /// a slot, and StopAsync can return before it gives one back, so a check made straight after either could race.
    /// </summary>
    private static async Task CountedAsync(HttpClient client, int connections)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline
            && (await (await client.GetAsync("/api/v1/usage")).ReadAsync<UsageReport>()).Limits.Single(l => l.Name == UsageLimits.Connections).Used != connections)
        {
            await Task.Delay(50);
        }
    }

    private static ConfiguredApiFactory Guarded(params (string Key, string Value)[] limits) =>
        new(new Dictionary<string, string?>(limits.Select(l => KeyValuePair.Create(l.Key, (string?)l.Value))) { ["UsageGuard:Enabled"] = "true" });

    [Fact]
    public async Task Replays_past_the_hourly_limit_are_429_with_retry_after()
    {
        await using var factory = Guarded(("UsageGuard:ReplaysPerHourPerIp", "2"));
        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 51, speed = 0.001 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 52, speed = 0.001 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var refused = await client.PostAsJsonAsync("/api/v1/replays", new { seed = 53, speed = 0.001 });

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        refused.Headers.RetryAfter!.Delta!.Value.ShouldBeInRange(TimeSpan.FromSeconds(1), TimeSpan.FromHours(1));
        var usage = await (await client.GetAsync("/api/v1/usage")).ReadAsync<UsageReport>();
        usage.Enforced.ShouldBeTrue();
        usage.Limits.Single(l => l.Name == UsageLimits.Replays).ShouldBe(new UsageCount(UsageLimits.Replays, "caller per hour", 2, 2));
        usage.ReplaysRunning.ShouldBe(2);
    }

    [Fact]
    public async Task Hub_connections_past_the_limit_are_refused()
    {
        await using var factory = Guarded(("UsageGuard:ConnectionsPerIp", "1"));
        HubConnection Connect() => new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/match"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await using var first = Connect();
        await first.StartAsync();
        await CountedAsync(factory.CreateClient(), 1);
        await using var second = Connect();
        var refused = await Should.ThrowAsync<HttpRequestException>(() => second.StartAsync());
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        await first.StopAsync();
        await CountedAsync(factory.CreateClient(), 0);
        await using var third = Connect();
        await third.StartAsync();
    }

    [Fact]
    public async Task A_trailing_slash_does_not_slip_past_either_limit()
    {
        await using var factory = Guarded(("UsageGuard:ReplaysPerHourPerIp", "1"), ("UsageGuard:ConnectionsPerIp", "1"));
        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 71, speed = 0.001 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await client.PostAsJsonAsync("/api/v1/replays/", new { seed = 72, speed = 0.001 })).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        await using var open = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/match"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        await open.StartAsync();
        await CountedAsync(client, 1);

        (await client.PostAsync(new Uri("/hubs/match/negotiate/?negotiateVersion=1", UriKind.Relative), null)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.TooManyRequests)]
    [InlineData("true", HttpStatusCode.Accepted)]
    public async Task A_forwarded_address_counts_only_where_the_ingress_is_trusted(string? trust, HttpStatusCode second)
    {
        await using var factory = trust is null
            ? Guarded(("UsageGuard:ReplaysPerHourPerIp", "1"))
            : Guarded(("UsageGuard:ReplaysPerHourPerIp", "1"), ("UsageGuard:TrustForwardedFor", trust));
        var client = factory.CreateClient();

        async Task<HttpStatusCode> ReplayFromAsync(string address, int seed)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/replays") { Content = JsonContent.Create(new { seed, speed = 0.001 }) };
            request.Headers.Add("X-Forwarded-For", address);
            return (await client.SendAsync(request)).StatusCode;
        }

        (await ReplayFromAsync("203.0.113.1", 61)).ShouldBe(HttpStatusCode.Accepted);
        (await ReplayFromAsync("203.0.113.2", 62)).ShouldBe(second);
    }

    [Fact]
    public async Task Connections_negotiated_before_any_opens_still_hold_one_slot_each()
    {
        await using var factory = Guarded(("UsageGuard:ConnectionsPerIp", "1"));
        var client = factory.CreateClient();

        async Task<string> NegotiateAsync()
        {
            var negotiated = await client.PostAsync(new Uri("/hubs/match/negotiate?negotiateVersion=1", UriKind.Relative), null);
            negotiated.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var body = System.Text.Json.JsonDocument.Parse(await negotiated.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("connectionToken").GetString()!;
        }

        // Both are negotiated while no connection is open, so the middleware lets both through.
        var tokens = new[] { await NegotiateAsync(), await NegotiateAsync() };
        // Long polling: the first poll opens the transport, then the handshake starts the hub connection.
        foreach (var token in tokens)
        {
            var hub = new Uri($"/hubs/match?id={token}", UriKind.Relative);
            (await client.GetAsync(hub)).StatusCode.ShouldBe(HttpStatusCode.OK);
            var handshake = new StringContent("{\"protocol\":\"json\",\"version\":1}\u001e");
            (await client.PostAsync(hub, handshake)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // The handshake reply can arrive before the hub admits a connection, and either may be admitted first, so poll
        // both until one is closed (a close message, or a poll ending with 204): one is refused while the other holds
        // the only slot.
        using var settled = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        async Task<bool> ClosesAsync(string token)
        {
            var hub = new Uri($"/hubs/match?id={token}", UriKind.Relative);
            try
            {
                while (true)
                {
                    var response = await client.GetAsync(hub, settled.Token);
                    if (response.StatusCode != HttpStatusCode.OK || (await response.Content.ReadAsStringAsync(settled.Token)).Contains("\"type\":7", StringComparison.Ordinal))
                    {
                        await settled.CancelAsync();
                        return true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        var closed = await Task.WhenAll(tokens.Select(ClosesAsync));
        closed.Count(c => c).ShouldBe(1);
        var usage = await (await client.GetAsync("/api/v1/usage")).ReadAsync<UsageReport>();
        usage.Limits.Single(l => l.Name == UsageLimits.Connections).Used.ShouldBe(1);
    }

    [Fact]
    public async Task Moments_past_the_hourly_narrative_limit_are_skipped()
    {
        await using var factory = Guarded(("UsageGuard:NarrativesPerHour", "3"));
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        UsageReport usage;
        do
        {
            await Task.Delay(250);
            usage = await (await client.GetAsync("/api/v1/usage")).ReadAsync<UsageReport>();
        }
        while (DateTime.UtcNow < deadline && usage.Limits.Single(l => l.Name == UsageLimits.Narratives).Used < 3);

        // Every later moment is refused without being counted, so the count stays at the limit.
        await Task.Delay(2_000);
        usage = await (await client.GetAsync("/api/v1/usage")).ReadAsync<UsageReport>();
        usage.Limits.Single(l => l.Name == UsageLimits.Narratives).Used.ShouldBe(3);
        (await (await client.GetAsync("/api/v1/matches/MER-7/narratives?persona=base&language=en")).ReadAsync<List<Narrative>>()).Count.ShouldBe(3);
    }

    [Fact]
    public async Task Limits_never_apply_to_the_scripted_provider_by_default()
    {
        await using var factory = new PitchsideApiFactory();
        var usage = await (await factory.CreateClient().GetAsync("/api/v1/usage")).ReadAsync<UsageReport>();

        usage.Enforced.ShouldBeFalse();
        usage.Limits.Select(l => l.Name).ShouldBe([UsageLimits.Replays, UsageLimits.Narratives, UsageLimits.SpokenLines, UsageLimits.Connections]);
    }
}
