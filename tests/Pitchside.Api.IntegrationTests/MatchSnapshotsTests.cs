using Akka.Actor;
using Microsoft.Extensions.DependencyInjection;
using Pitchside.Api.Broadcast;
using Pitchside.Api.Hubs;
using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.IntegrationTests;

public sealed class MatchSnapshotsTests
{
    [Fact]
    public async Task A_match_actor_too_busy_to_answer_gets_a_snapshot_from_the_store()
    {
        await using var factory = new PitchsideApiFactory();
        var store = factory.Services.GetRequiredService<IMatchStore>();
        var match = MatchSimulator.Simulate(41);
        await store.TryCreateMatchAsync(new StoredMatch(match.Document, MatchStatus.Live, 12, DateTimeOffset.UnixEpoch), CancellationToken.None);
        var sheet = new FactSheet(match.Document.MatchId, 10, 1, 60_000, []);
        await store.SaveFactSheetAsync(sheet, CancellationToken.None);

        using var system = ActorSystem.Create("snapshots-test");
        var silent = system.ActorOf(Props.Create(() => new SilentActor()));
        var snapshots = new MatchSnapshots(store, new FixedRequiredActor<MatchSupervisorActor>(silent), factory.Services.GetRequiredService<OverlayCoordinator>());

        var snapshot = await snapshots.BuildAsync(match.Document.MatchId, new ViewerPreferences(Personas.Base, Languages.English), CancellationToken.None);

        snapshot.ShouldNotBeNull();
        snapshot.Match.Status.ShouldBe(MatchStatus.Live);
        snapshot.FactSheet!.AsOfSeq.ShouldBe(10);
        await system.Terminate();
    }
}
