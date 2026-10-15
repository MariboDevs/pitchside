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
        connection.On<OverlayEntry>("OverlayEntry", e => received.Enqueue(("overlay", e.InAtMs)));

        await connection.StartAsync();
        var snapshot = await connection.InvokeAsync<MatchSnapshot>("JoinMatch", "MER-11");
        snapshot.Match.MatchId.ShouldBe("MER-11");
        snapshot.FactSheet.ShouldNotBeNull();
        snapshot.ApprovalMode.ShouldBe(ApprovalMode.Auto);

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline && !(received.Any(r => r.Kind == "moment") && received.Any(r => r.Kind == "narrative") && received.Any(r => r.Kind == "overlay") && received.Count(r => r.Kind == "facts") > 3))
        {
            await Task.Delay(100);
        }

        // Where the pipeline stood, so a failure on a loaded runner says whether ingestion or the broadcast stalled.
        var matches = await _factory.Services.GetRequiredService<IRequiredActor<MatchSupervisorActor>>().GetAsync();
        var state = await matches.Ask<MatchStateView>(new GetState("MER-11"), TimeSpan.FromSeconds(5));
        var replaying = _factory.Services.GetRequiredService<Pitchside.Runtime.Replay.IReplayService>().Running;
        var diagnostics = $"joined at seq {snapshot.Match.LastSeq}; actor {state.Status} at seq {state.LastSeq} with {state.Subscribers} subscribers; "
            + $"replaying [{string.Join(", ", replaying)}]; received {received.Count} messages";

        await client.DeleteAsync("/api/v1/replays/MER-11");
        var log = received.ToList();
        log.ShouldContain(r => r.Kind == "event", diagnostics);
        log.ShouldContain(r => r.Kind == "facts", diagnostics);
        log.ShouldContain(r => r.Kind == "moment", diagnostics);
        log.ShouldContain(r => r.Kind == "narrative", diagnostics);
        log.ShouldContain(r => r.Kind == "overlay", diagnostics);

        // A fact sheet or moment can arrive before the first event this connection receives: its event was pushed
        // just before the connection joined, and the snapshot carries it. Order is checked from the first event on.
        var lastEvent = 0;
        foreach (var (kind, seq) in log)
        {
            switch (kind)
            {
                case "event":
                    seq.ShouldBeGreaterThan(lastEvent);
                    lastEvent = seq;
                    break;
                case "facts" or "moment" when lastEvent == 0:
                    seq.ShouldBeLessThanOrEqualTo(log.First(r => r.Kind == "event").Seq, $"{kind} at {seq} came before every event received");
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
    public async Task Each_connection_receives_only_the_narratives_its_viewer_preferences_match()
    {
        (await _factory.CreateClient().PostAsJsonAsync("/api/v1/replays", new { seed = 13, speed = 0 })).EnsureSuccessStatusCode();
        var viewer = new ViewerPreferences(Personas.Analyst, Languages.Afrikaans);
        var control = new ViewerPreferences(Personas.All, Languages.English);
        var toViewer = new ConcurrentQueue<Narrative>();
        var toControl = new ConcurrentQueue<Narrative>();
        await using var viewerConnection = Connect(toViewer);
        await using var controlConnection = Connect(toControl);
        await viewerConnection.StartAsync();
        await controlConnection.StartAsync();
        await viewerConnection.InvokeAsync("SetViewer", "MER-13", viewer);
        await controlConnection.InvokeAsync("SetViewer", "MER-13", control);
        // A join is complete after its snapshot (the hub's contract): narratives published before it are in the
        // snapshot, the rest arrive live. At speed 0 most can be published before the join, so both are counted.
        foreach (var narrative in (await viewerConnection.InvokeAsync<MatchSnapshot>("JoinMatch", "MER-13")).Narratives)
        {
            toViewer.Enqueue(narrative);
        }

        foreach (var narrative in (await controlConnection.InvokeAsync<MatchSnapshot>("JoinMatch", "MER-13")).Narratives)
        {
            toControl.Enqueue(narrative);
        }

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline && !(toViewer.Any(n => n.Persona == Personas.Analyst) && toControl.Any(n => n.Persona == Personas.ClubFan && n.Language == Languages.Spanish)))
        {
            await Task.Delay(100);
        }

        toViewer.ShouldContain(n => n.Persona == Personas.Analyst && n.Language == Languages.Afrikaans);
        toViewer.ShouldAllBe(n => (n.Persona == Personas.Analyst && n.Language == Languages.Afrikaans)
            || (n.Persona == Personas.Base && (n.Language == Languages.Afrikaans || n.Language == Languages.English)));
        toControl.Select(n => n.Persona).Distinct().Count().ShouldBeGreaterThan(3);
        toControl.Select(n => n.Language).Distinct().Count().ShouldBe(4);
    }

    private HubConnection Connect(ConcurrentQueue<Narrative> received)
    {
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
        connection.On<Narrative>("Narrative", received.Enqueue);
        return connection;
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
