using System.Net;
using System.Net.Http.Json;
using Pitchside.Api.Endpoints;
using Pitchside.Contracts;

namespace Pitchside.Api.IntegrationTests;

public sealed class TraceTests : IClassFixture<PitchsideApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private List<Narrative> _narratives = [];

    public TraceTests(PitchsideApiFactory factory) => _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 });
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Accepted, HttpStatusCode.Conflict);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && _narratives.Count < 752)
        {
            _narratives = await (await _client.GetAsync("/api/v1/matches/MER-7/narratives")).ReadAsync<List<Narrative>>();
            await Task.Delay(200);
        }

        _narratives.Count.ShouldBe(752);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Every_verified_narratives_trace_resolves_to_facts_and_events()
    {
        foreach (var narrative in _narratives.Where(n => n.Status == NarrativeStatus.Verified))
        {
            var trace = await (await _client.GetAsync($"/api/v1/matches/MER-7/narratives/{narrative.Id}/trace")).ReadAsync<NarrativeTrace>();
            trace.Sentences.Select(s => s.Text).ShouldBe(narrative.Sentences.Select(s => s.Text));
            var citations = trace.Sentences.SelectMany(s => s.Citations).ToList();
            citations.ShouldAllBe(c => c.Fact != null, narrative.Id);
            var eventIds = trace.Events.Select(e => e.Id).ToHashSet();
            citations.SelectMany(c => c.Fact!.DerivedFrom).ShouldAllBe(id => eventIds.Contains(id), narrative.Id);
        }
    }

    [Fact]
    public async Task A_cited_fact_carries_its_value_and_its_events()
    {
        var goal = _narratives.First(n => n.Id == "MER-7:M:0324:goal:base:en");
        var trace = await (await _client.GetAsync($"/api/v1/matches/MER-7/narratives/{goal.Id}/trace")).ReadAsync<NarrativeTrace>();

        trace.FactSheetSeq.ShouldBe(goal.FactSheetSeq);
        var score = trace.Sentences.SelectMany(s => s.Citations).First(c => c.Key == "match.score.away");
        score.Fact!.Value.Number.ShouldBe(1);
        trace.Events.ShouldContain(e => e.Type == EventType.Goal && score.Fact.DerivedFrom.Contains(e.Id));
    }

    [Fact]
    public async Task An_unknown_narrative_or_match_is_not_found()
    {
        (await _client.GetAsync("/api/v1/matches/MER-7/narratives/nope/trace")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.GetAsync("/api/v1/matches/MER-404/narratives/nope/trace")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
