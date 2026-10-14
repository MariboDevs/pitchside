using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Akka.Actor;
using Akka.Hosting;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;

namespace Pitchside.Api.IntegrationTests;

public sealed class MatchHubTests : IClassFixture<PitchsideApiFactory>
{
    private readonly PitchsideApiFactory _factory;

    public MatchHubTests(PitchsideApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_client_joins_a_replay_and_receives_events_fact_sheets_and_moments_in_order()
    {
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/replays", new { seed = 11, speed = 200 })).EnsureSuccessStatusCode();

        var received = new ConcurrentQueue<(string Kind, int Seq)>();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/match"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(options =>
            {
                foreach (var converter in ContractJson.Options.Converters)
                {
                    options.PayloadSerializerOptions.Converters.Add(converter);
                }
            })
            .Build();
        connection.On<MatchEvent>("Event", e => received.Enqueue(("event", e.Seq)));
        connection.On<FactSheet>("FactSheet", s => received.Enqueue(("facts", s.AsOfSeq)));
        connection.On<Moment>("Moment", m => received.Enqueue(("moment", m.TriggerSeq)));
        connection.On<Narrative>("Narrative", n => received.Enqueue(("narrative", n.FactSheetSeq)));

        await connection.StartAsync();
        var snapshot = await connection.InvokeAsync<MatchSnapshot>("JoinMatch", "MER-11");
        snapshot.Match.MatchId.ShouldBe("MER-11");
        snapshot.FactSheet.ShouldNotBeNull();

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline && !(received.Any(r => r.Kind == "moment") && received.Any(r => r.Kind == "narrative") && received.Count(r => r.Kind == "facts") > 3))
        {
            await Task.Delay(100);
        }

        await client.DeleteAsync("/api/v1/replays/MER-11");
        var log = received.ToList();
        log.ShouldContain(r => r.Kind == "event");
        log.ShouldContain(r => r.Kind == "facts");
        log.ShouldContain(r => r.Kind == "moment");
        log.ShouldContain(r => r.Kind == "narrative");

        var lastEvent = 0;
        foreach (var (kind, seq) in log)
        {
            switch (kind)
            {
                case "event":
                    seq.ShouldBeGreaterThan(lastEvent);
                    lastEvent = seq;
                    break;
                case "facts" or "moment":
                    seq.ShouldBe(lastEvent, $"{kind} at {seq} must follow its event");
                    break;
                default:
                    break;
            }
        }
    }

    [Fact]
    public async Task Joining_an_unknown_match_fails()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/match"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        await connection.StartAsync();

        await Should.ThrowAsync<Microsoft.AspNetCore.SignalR.HubException>(() => connection.InvokeAsync<MatchSnapshot>("JoinMatch", "MER-404"));
        await Should.ThrowAsync<Microsoft.AspNetCore.SignalR.HubException>(() => connection.InvokeAsync<MatchSnapshot>("JoinMatch", "a/b"));
        await connection.InvokeAsync("LeaveMatch", "a/b");
    }

    [Fact]
    public async Task A_disconnect_unsubscribes_from_every_joined_match()
    {
        (await _factory.CreateClient().PostAsJsonAsync("/api/v1/replays", new { seed = 12, speed = 0 })).EnsureSuccessStatusCode();
        var matches = await _factory.Services.GetRequiredService<IRequiredActor<MatchSupervisorActor>>().GetAsync();
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/match"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(options =>
            {
                foreach (var converter in ContractJson.Options.Converters)
                {
                    options.PayloadSerializerOptions.Converters.Add(converter);
                }
            })
            .Build();
        await connection.StartAsync();
        await connection.InvokeAsync<MatchSnapshot>("JoinMatch", "MER-12");
        (await matches.Ask<MatchStateView>(new GetState("MER-12"), TimeSpan.FromSeconds(5))).Subscribers.ShouldBe(1);

        await connection.DisposeAsync();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        var subscribers = -1;
        while (DateTime.UtcNow < deadline && subscribers != 0)
        {
            subscribers = (await matches.Ask<MatchStateView>(new GetState("MER-12"), TimeSpan.FromSeconds(5))).Subscribers;
            await Task.Delay(100);
        }

        subscribers.ShouldBe(0);
    }

    [Fact]
    public async Task Viewer_preferences_are_kept_per_connection()
    {
        var viewers = _factory.Services.GetRequiredService<Hubs.ViewerRegistry>();
        viewers.Set("connection-1", "MER-7", new ViewerPreferences(Personas.Analyst, Languages.Spanish));
        viewers.Get("connection-1", "MER-7").Persona.ShouldBe(Personas.Analyst);
        viewers.Get("connection-2", "MER-7").ShouldBe(ViewerPreferences.Default);
        viewers.RemoveConnection("connection-1");
        viewers.Get("connection-1", "MER-7").ShouldBe(ViewerPreferences.Default);
    }
}
