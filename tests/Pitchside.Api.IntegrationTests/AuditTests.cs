using System.Net;
using System.Net.Http.Json;
using Pitchside.Api.Endpoints;
using Pitchside.Contracts;

namespace Pitchside.Api.IntegrationTests;

public sealed class AuditTests : IClassFixture<PitchsideApiFactory>, IAsyncLifetime
{
    private readonly PitchsideApiFactory _factory;
    private readonly HttpClient _client;

    public AuditTests(PitchsideApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/replays", new { seed = 7, speed = 0 });
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Accepted, HttpStatusCode.Conflict);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var narratives = await (await _client.GetAsync("/api/v1/matches/MER-7/narratives?persona=base&language=en")).ReadAsync<List<Narrative>>();
            if (narratives.Count == 34)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"The crew did not narrate MER-7. {await MatchProgress.DescribeAsync(_factory.Services, "MER-7")}");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Every_model_call_of_a_match_is_audited_and_pages_in_call_order()
    {
        var all = new List<AuditRecord>();
        string? after = null;
        do
        {
            var url = "/api/v1/audit?matchId=MER-7&limit=200" + (after is null ? string.Empty : $"&after={after}");
            var page = await (await _client.GetAsync(url)).ReadAsync<AuditPage>();
            all.AddRange(page.Items);
            after = page.Next;
        }
        while (after is not null);

        // Every base narrative's explainer ran at least once for each of the 34 moments.
        all.Where(r => r.Stage == "explainer").Select(r => r.MomentId).Distinct().Count().ShouldBe(34);
        all.ShouldContain(r => r.Stage == "translator");
        all.ShouldAllBe(r => r.MatchId == "MER-7" && r.Provider == "scripted" && r.PromptHash.Length == 64 && r.Outcome == AuditOutcome.Ok);
        all.Select(r => r.Id).ShouldBe(all.Select(r => r.Id).Order(StringComparer.Ordinal));
        all.Select(r => r.Id).Distinct().Count().ShouldBe(all.Count);
    }

    [Fact]
    public async Task The_audit_needs_a_known_match()
    {
        (await _client.GetAsync("/api/v1/audit")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.GetAsync("/api/v1/audit?matchId=MER-7&limit=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.GetAsync("/api/v1/audit?matchId=MER-404")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
