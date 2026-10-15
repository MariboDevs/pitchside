using Akka.Actor;
using Microsoft.Extensions.DependencyInjection;
using Pitchside.Api.Broadcast;
using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Overlay;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Replay;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.IntegrationTests;

public sealed class OverlayCoordinatorTests
{
    [Fact]
    public async Task A_match_actor_too_busy_to_answer_does_not_fail_the_timeline_read()
    {
        await using var factory = new PitchsideApiFactory();
        var services = factory.Services;
        var store = services.GetRequiredService<IMatchStore>();
        var match = MatchSimulator.Simulate(42);
        await store.TryCreateMatchAsync(new StoredMatch(match.Document, MatchStatus.Completed, 0, DateTimeOffset.UnixEpoch), CancellationToken.None);

        using var system = ActorSystem.Create("overlay-test");
        var silent = system.ActorOf(Props.Create(() => new SilentActor()));
        var overlay = new OverlayCoordinator(
            new OverlayBook(), store, services.GetRequiredService<IMatchBroadcaster>(), new FixedRequiredActor<MatchSupervisorActor>(silent), services.GetRequiredService<IReplayService>());

        (await overlay.EntriesAsync(match.Document.MatchId, CancellationToken.None)).ShouldBeEmpty();
        await system.Terminate();
    }

    [Fact]
    public async Task A_hold_or_an_air_is_refused_rather_than_timed_against_a_clock_that_is_behind()
    {
        await using var factory = new PitchsideApiFactory();
        var services = factory.Services;
        var store = services.GetRequiredService<IMatchStore>();
        var match = MatchSimulator.Simulate(42);
        await store.TryCreateMatchAsync(new StoredMatch(match.Document, MatchStatus.Live, 0, DateTimeOffset.UnixEpoch), CancellationToken.None);

        using var system = ActorSystem.Create("overlay-control-test");
        var silent = system.ActorOf(Props.Create(() => new SilentActor()));
        var overlay = new OverlayCoordinator(
            new OverlayBook(), store, services.GetRequiredService<IMatchBroadcaster>(), new FixedRequiredActor<MatchSupervisorActor>(silent), services.GetRequiredService<IReplayService>());

        var matchId = match.Document.MatchId;
        await Should.ThrowAsync<AskTimeoutException>(() => overlay.AirAsync(matchId, "card", CancellationToken.None));
        await Should.ThrowAsync<AskTimeoutException>(() => overlay.HoldAsync(matchId, "card", CancellationToken.None));
        (await store.GetOverlayEntriesAsync(matchId, CancellationToken.None)).ShouldBeEmpty();
        await system.Terminate();
    }
}
