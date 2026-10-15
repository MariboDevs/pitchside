using System.Net;
using System.Net.Http.Json;
using Pitchside.Contracts;

namespace Pitchside.Api.IntegrationTests;

public sealed class GroundednessTests
{
    [Fact]
    public async Task Verified_narratives_are_scored_in_batches_and_stored_with_their_score()
    {
        await using var factory = new ConfiguredApiFactory(new Dictionary<string, string?> { ["Evaluation:BatchSeconds"] = "1" });
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var scored = new List<Narrative>();
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            var base_ = await (await client.GetAsync("/api/v1/matches/MER-7/narratives?persona=base&language=en")).ReadAsync<List<Narrative>>();
            scored = [.. base_.Where(n => n.Evaluation.Groundedness is not null)];
            if (base_.Count == 34 && scored.Count == base_.Count(n => n.Status == NarrativeStatus.Verified))
            {
                break;
            }

            await Task.Delay(250);
        }

        // The scripted crew verifies every seed-7 base narrative, so every one of the 34 must end up scored.
        scored.Count.ShouldBe(34);
        scored.ShouldAllBe(n => n.Status == NarrativeStatus.Verified);
        scored.ShouldAllBe(n => n.Evaluation.Groundedness >= 0 && n.Evaluation.Groundedness <= 1 && n.Evaluation.CitationCoverage == 1);
    }
}
