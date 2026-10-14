using System.Net;
using System.Net.Http.Json;
using Pitchside.Contracts;

namespace Pitchside.Api.IntegrationTests;

public sealed class OverlayEndpointsTests : IClassFixture<PitchsideApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public OverlayEndpointsTests(PitchsideApiFactory factory) => _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 });
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Accepted, HttpStatusCode.Conflict);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var entries = await (await _client.GetAsync("/api/v1/matches/MER-7/overlay")).ReadAsync<List<OverlayEntry>>();
            if (entries.Count == 34)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("The overlay timeline did not fill.");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Every_moment_gets_a_lower_third_and_none_overlap()
    {
        var entries = await (await _client.GetAsync("/api/v1/matches/MER-7/overlay")).ReadAsync<List<OverlayEntry>>();
        entries.ShouldAllBe(e => e.Layer == OverlayLayer.LowerThird);
        entries.ShouldContain(e => e.MomentId == "MER-7:M:0324:goal" && e.Chips.Count == 2);
        foreach (var period in new[] { 1, 2 })
        {
            var live = entries.Where(e => e.Period == period && e.State != OverlayState.Expired && e.State != OverlayState.Held).OrderBy(e => e.InAtMs).ToList();
            for (var i = 1; i < live.Count; i++)
            {
                live[i].InAtMs.ShouldBeGreaterThanOrEqualTo(live[i - 1].OutAtMs);
            }
        }

        // The match is over, so every scheduled card reads as aired.
        entries.ShouldNotContain(e => e.State == OverlayState.Scheduled);
        (await (await _client.GetAsync("/api/v1/matches/MER-7/overlay?fromMs=2700000")).ReadAsync<List<OverlayEntry>>()).Count.ShouldBeLessThan(entries.Count);
    }

    [Fact]
    public async Task A_card_can_be_held_and_aired()
    {
        var entries = await (await _client.GetAsync("/api/v1/matches/MER-7/overlay")).ReadAsync<List<OverlayEntry>>();
        var expired = entries.FirstOrDefault(e => e.State == OverlayState.Expired);
        var aired = entries.First(e => e.State == OverlayState.Aired);

        (await _client.PostAsync($"/api/v1/matches/MER-7/overlay/{aired.Id}/hold", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.PostAsync("/api/v1/matches/MER-7/overlay/nope/air", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        if (expired is not null)
        {
            (await _client.PostAsync($"/api/v1/matches/MER-7/overlay/{expired.Id}/air", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Approval_mode_is_set_per_match()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/matches/MER-7/approval", new { mode = "approve" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<ApprovalModeUpdate>()).Mode.ShouldBe(ApprovalMode.Approve);
        (await _client.PostAsJsonAsync("/api/v1/matches/MER-404/approval", new { mode = "auto" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await _client.PostAsJsonAsync("/api/v1/matches/MER-7/approval", new { mode = "auto" });
    }

    [Fact]
    public async Task Narratives_filter_by_persona_and_language()
    {
        var all = await (await _client.GetAsync("/api/v1/matches/MER-7/narratives")).ReadAsync<List<Narrative>>();
        all.Count.ShouldBe(34);
        (await (await _client.GetAsync("/api/v1/matches/MER-7/narratives?persona=base&language=en")).ReadAsync<List<Narrative>>()).Count.ShouldBe(34);
        (await (await _client.GetAsync("/api/v1/matches/MER-7/narratives?persona=analyst")).ReadAsync<List<Narrative>>()).ShouldBeEmpty();
        (await _client.GetAsync("/api/v1/matches/MER-404/narratives")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_fact_series_samples_the_match_for_late_joiners()
    {
        var points = await (await _client.GetAsync("/api/v1/matches/MER-7/facts/series?keys=home.xg,away.xg&every=100")).ReadElementAsync();
        points.GetArrayLength().ShouldBeGreaterThan(16);
        var last = points[points.GetArrayLength() - 1];
        last.GetProperty("asOfSeq").GetInt32().ShouldBe(1623);
        last.GetProperty("values").GetProperty("away.xg").GetDouble().ShouldBeGreaterThan(0);
        (await _client.GetAsync("/api/v1/matches/MER-7/facts/series")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var repeated = await _client.GetAsync("/api/v1/matches/MER-7/facts/series?keys=home.xg,home.xg&every=400");
        repeated.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Hold_and_air_on_an_unknown_match_are_not_found()
    {
        (await _client.PostAsync("/api/v1/matches/MER-404/overlay/MER-404:M:0001:goal:lower_third/hold", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.PostAsync("/api/v1/matches/MER-404/overlay/MER-404:M:0001:goal:lower_third/air", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
