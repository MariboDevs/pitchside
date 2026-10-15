using System.Net;
using System.Net.Http.Json;

namespace Pitchside.Api.IntegrationTests;

public sealed class ReplayLimitTests
{
    [Fact]
    public async Task A_replay_beyond_the_cap_is_429()
    {
        await using var factory = new ConfiguredApiFactory(new Dictionary<string, string?> { ["Replay:MaxConcurrent"] = "1" });
        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 41, speed = 0.001 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var refused = await client.PostAsJsonAsync("/api/v1/replays", new { seed = 42, speed = 0.001 });

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        refused.Headers.RetryAfter!.Delta.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Live_mode_has_no_replay_endpoints()
    {
        await using var factory = new ConfiguredApiFactory(new Dictionary<string, string?> { ["Pitchside:Mode"] = "Live" });
        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 41, speed = 0 })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/v1/matches")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
