using System.Net;
using System.Net.Http.Json;
using Pitchside.Contracts;
using Pitchside.Runtime.Replay;

namespace Pitchside.Api.IntegrationTests;

public sealed class DemoTests
{
    [Fact]
    public async Task With_auto_start_on_the_demo_seed_is_being_played_when_the_host_is_up()
    {
        await using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["Demo:AutoStart"] = "true",
            ["Demo:Seed"] = "26",
            ["Demo:Speed"] = "0",
        });
        using var client = factory.CreateClient();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        HttpResponseMessage match;
        while ((match = await client.GetAsync("/api/v1/matches/MER-26")).StatusCode != HttpStatusCode.OK)
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "the demo did not start within 30 s");
            await Task.Delay(100);
        }

        (await match.ReadAsync<StoredMatch>()).Document.MatchId.ShouldBe("MER-26");
    }

    [Fact]
    public async Task A_jump_names_the_next_moment_and_only_a_running_replay_can_jump()
    {
        await using var factory = new PitchsideApiFactory();
        using var client = factory.CreateClient();
        (await client.PostAsync("/api/v1/replays/MER-404/jump", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 110, speed = 1 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while ((await (await client.GetAsync("/api/v1/matches/MER-110/events?fromSeq=3&toSeq=3")).ReadAsync<List<MatchEvent>>()).Count == 0)
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "seq 3 was not published within 10 s");
            await Task.Delay(50);
        }

        var response = await client.PostAsync("/api/v1/replays/MER-110/jump", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var jump = await response.ReadAsync<ReplayJump>();
        jump.MomentId.ShouldBe("MER-110:M:0036:momentum_swing");
        jump.MomentType.ShouldBe("momentum_swing");
        (await client.DeleteAsync("/api/v1/replays/MER-110")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
