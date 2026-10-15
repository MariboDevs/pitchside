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
        await using var second = Connect();
        var refused = await Should.ThrowAsync<HttpRequestException>(() => second.StartAsync());
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        await first.StopAsync();
        await using var third = Connect();
        await third.StartAsync();
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
        foreach (var token in tokens)
        {
            // Long polling: the first poll opens the transport, then the handshake starts the hub connection, whose
            // reply arrives on the next poll (a refused connection's poll ends with 204 instead).
            var hub = new Uri($"/hubs/match?id={token}", UriKind.Relative);
            (await client.GetAsync(hub)).StatusCode.ShouldBe(HttpStatusCode.OK);
            var handshake = new StringContent("{\"protocol\":\"json\",\"version\":1}\u001e");
            (await client.PostAsync(hub, handshake)).StatusCode.ShouldBe(HttpStatusCode.OK);
            using var poll = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.GetAsync(hub, poll.Token);
        }

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
