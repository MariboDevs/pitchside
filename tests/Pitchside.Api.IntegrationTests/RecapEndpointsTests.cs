using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Json.Schema;
using Pitchside.Contracts;

namespace Pitchside.Api.IntegrationTests;

public sealed class RecapEndpointsTests
{
    private static readonly Dictionary<string, string?> Spoken = new() { ["Speech:Provider"] = "Scripted" };

    [Fact]
    public async Task With_speech_off_the_recap_is_not_found_and_says_why()
    {
        await using var factory = new PitchsideApiFactory();
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/api/v1/matches/MER-7/recap", "/api/v1/matches/MER-7/recap/audio?language=zu" })
        {
            var response = await client.GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await response.ReadElementAsync()).GetProperty("title").GetString().ShouldBe("Speech is off");
        }
    }

    [Fact]
    public async Task At_full_time_each_language_has_a_recap_and_its_mp3()
    {
        await using var factory = new ConfiguredApiFactory(Spoken);
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/v1/matches/MER-7/recap")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 })).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var schema = JsonSchema.FromText(File.ReadAllText(RepositoryRoot.Combine("schemas", "recap.schema.json")));
        foreach (var language in Languages.All)
        {
            var response = await WaitForRecapAsync(client, factory.Services, language);
            var json = await response.ReadElementAsync();
            schema.Evaluate(json).IsValid.ShouldBeTrue(json.GetRawText());
            var recap = json.Deserialize<Recap>(ContractJson.Options)!;
            recap.Language.ShouldBe(language);
            recap.MomentId.ShouldEndWith(":full_time");

            var audio = await client.GetAsync($"/api/v1/matches/MER-7/recap/audio?language={language}");
            audio.StatusCode.ShouldBe(HttpStatusCode.OK);
            audio.Content.Headers.ContentType!.MediaType.ShouldBe("audio/mpeg");
            (await audio.Content.ReadAsByteArrayAsync()).Take(2).ShouldBe(new byte[] { 0xFF, 0xFB });
        }
    }

    [Fact]
    public async Task An_unknown_language_is_invalid_and_an_unknown_match_is_not_found()
    {
        await using var factory = new ConfiguredApiFactory(Spoken);
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/v1/matches/MER-7/recap?language=fr")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var unknown = await client.GetAsync("/api/v1/matches/MER-404/recap/audio");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unknown.ReadElementAsync()).GetProperty("title").GetString().ShouldBe("No such match");
    }

    private static async Task<HttpResponseMessage> WaitForRecapAsync(HttpClient client, IServiceProvider services, string language)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var response = await client.GetAsync($"/api/v1/matches/MER-7/recap?language={language}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return response;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"No {language} recap within 30 s. {await MatchProgress.DescribeAsync(services, "MER-7")}");
            }

            await Task.Delay(200);
        }
    }
}
