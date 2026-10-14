using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Pitchside.Contracts;
using Pitchside.Runtime.Replay;

namespace Pitchside.Api.IntegrationTests;

public sealed class MatchEndpointsTests : IClassFixture<PitchsideApiFactory>, IAsyncLifetime
{
    private readonly PitchsideApiFactory _factory;
    private readonly HttpClient _client;

    public MatchEndpointsTests(PitchsideApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // One completed replay of seed 7 shared by the read tests.
        var response = await _client.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 });
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Accepted, HttpStatusCode.Conflict);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        StoredMatch? match = null;
        while (DateTime.UtcNow < deadline)
        {
            match = await (await _client.GetAsync("/api/v1/matches/MER-7")).ReadAsync<StoredMatch>();
            if (match.Status == MatchStatus.Completed)
            {
                return;
            }

            await Task.Delay(100);
        }

        // Say where it stopped, so a failure on a loaded runner can be told apart from a stuck actor.
        var replaying = _factory.Services.GetRequiredService<IReplayService>().Running;
        throw new TimeoutException(
            $"MER-7 did not complete: stored status {match?.Status}, last seq {match?.LastSeq}, replaying [{string.Join(", ", replaying)}].");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Clubs_are_the_fictional_league()
    {
        var clubs = await (await _client.GetAsync("/api/v1/clubs")).ReadAsync<List<Club>>();
        clubs.Count.ShouldBeGreaterThan(10);
        clubs.ShouldContain(c => c.Id == "AMZ");
    }

    [Fact]
    public async Task A_simulation_is_available_by_seed()
    {
        (await (await _client.GetAsync("/api/v1/simulations/7")).ReadAsync<MatchDocument>()).MatchId.ShouldBe("MER-7");
        (await (await _client.GetAsync("/api/v1/simulations/7/events")).ReadAsync<List<MatchEvent>>()).Count.ShouldBe(1623);
        (await _client.GetAsync("/api/v1/simulations/0")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Matches_are_listed_and_read()
    {
        var matches = await (await _client.GetAsync("/api/v1/matches")).ReadAsync<List<StoredMatch>>();
        matches.ShouldContain(m => m.MatchId == "MER-7");
        var match = await (await _client.GetAsync("/api/v1/matches/MER-7")).ReadAsync<StoredMatch>();
        match.LastSeq.ShouldBe(1623);
        match.Document.Home.Club.Name.ShouldBe("Amanzi United");
    }

    [Fact]
    public async Task An_unknown_match_is_a_problem_details_404_with_a_request_id()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/matches/MER-404");
        request.Headers.Add("X-Request-ID", "test-request-1");
        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.GetValues("X-Request-ID").ShouldBe(["test-request-1"]);
        (await _client.GetAsync("/api/v1/matches/MER-404/moments")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.GetAsync("/api/v1/matches/MER-404/events")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.GetAsync("/api/v1/matches/MER-404/facts")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("forged\r\nINFO: admin signed in")]
    [InlineData("trailing-newline\n")]
    [InlineData("has spaces")]
    [InlineData("a-very-long-id-0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890")]
    public async Task A_request_id_that_is_not_a_plain_token_is_replaced(string given)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/clubs");
        request.Headers.TryAddWithoutValidation("X-Request-ID", given);
        var response = await _client.SendAsync(request);

        var returned = response.Headers.GetValues("X-Request-ID").Single();
        returned.ShouldNotBe(given);
        returned.Length.ShouldBe(32);
    }

    [Fact]
    public async Task A_request_without_an_id_gets_one()
    {
        var response = await _client.GetAsync("/api/v1/clubs");
        response.Headers.GetValues("X-Request-ID").Single().Length.ShouldBe(32);
    }

    [Fact]
    public async Task Events_are_paged_by_seq()
    {
        var events = await (await _client.GetAsync("/api/v1/matches/MER-7/events?fromSeq=10&toSeq=19")).ReadAsync<List<MatchEvent>>();
        events.Select(e => e.Seq).ShouldBe(Enumerable.Range(10, 10));
        (await _client.GetAsync("/api/v1/matches/MER-7/events?fromSeq=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.GetAsync("/api/v1/matches/MER-7/events?fromSeq=20&toSeq=10")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Facts_are_the_live_sheet_or_recomputed_at_a_seq()
    {
        var latest = await (await _client.GetAsync("/api/v1/matches/MER-7/facts")).ReadAsync<FactSheet>();
        latest.AsOfSeq.ShouldBe(1623);
        latest.Find("match.state")!.Value.Text.ShouldBe("full_time");

        var at = await (await _client.GetAsync("/api/v1/matches/MER-7/facts?atSeq=812")).ReadAsync<FactSheet>();
        at.AsOfSeq.ShouldBe(812);
        at.Facts.Count.ShouldBeGreaterThan(120);
    }

    [Fact]
    public async Task Moments_are_the_detected_list()
    {
        var moments = await (await _client.GetAsync("/api/v1/matches/MER-7/moments")).ReadAsync<List<Moment>>();
        moments.Count.ShouldBe(34);
        moments[0].Type.ShouldBe("kick_off");
        moments[^1].Type.ShouldBe("full_time");
    }

    [Fact]
    public async Task Replays_validate_conflict_and_control()
    {
        (await _client.PostAsJsonAsync("/api/v1/replays", new { seed = 0, speed = 0 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var started = await (await _client.PostAsJsonAsync("/api/v1/replays", new { seed = 31, speed = 0.001 })).ReadAsync<ReplayStarted>();
        started.MatchId.ShouldBe("MER-31");
        (await _client.PostAsync("/api/v1/replays/MER-31/pause", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.PostAsync("/api/v1/replays/MER-31/resume", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.DeleteAsync("/api/v1/replays/MER-31")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.PostAsync("/api/v1/replays/MER-404/pause", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.PostAsync("/api/v1/replays/MER-404/resume", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.DeleteAsync("/api/v1/replays/MER-404")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
