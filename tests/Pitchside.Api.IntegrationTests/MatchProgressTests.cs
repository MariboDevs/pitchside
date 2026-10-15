using System.Net;
using System.Net.Http.Json;
using Pitchside.Contracts;

namespace Pitchside.Api.IntegrationTests;

public sealed class MatchProgressTests
{
    [Fact]
    public async Task The_stall_diagnostic_reads_every_stage_of_a_played_match()
    {
        await using var factory = new PitchsideApiFactory();
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 43, speed = 0 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && (await (await client.GetAsync("/api/v1/matches/MER-43")).ReadAsync<StoredMatch>()).Status != MatchStatus.Completed)
        {
            await Task.Delay(100);
        }

        var described = await MatchProgress.DescribeAsync(factory.Services, "MER-43");

        described.ShouldContain("stored status Completed");
        described.ShouldContain("bus 0 unread");
        described.ShouldContain("ingest consumer WaitingForActivation");
        described.ShouldContain("actor running, status Completed");
        described.ShouldContain("pending 0");
    }
}
