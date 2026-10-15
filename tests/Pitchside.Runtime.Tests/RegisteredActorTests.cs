using Akka.Actor;
using Akka.Hosting;
using Pitchside.Runtime.Actors;

namespace Pitchside.Runtime.Tests;

public sealed class RegisteredActorTests
{
    [Fact]
    public async Task A_wait_ends_when_the_actor_is_registered_however_the_two_interleave()
    {
        using var system = ActorSystem.Create("registered-actor-test");
        var actor = system.ActorOf(Props.Empty);

        // Registration racing the wait, many times over: Akka.Hosting's GetAsync can miss a registration that lands
        // between its first look and its waiter being added; this wait must never miss one.
        for (var i = 0; i < 2_000; i++)
        {
            var registry = new ActorRegistry();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var waiting = Task.Run(() => RegisteredActor.WaitAsync<MatchSupervisorActor>(registry, timeout.Token));
            var registering = Task.Run(() => registry.Register<MatchSupervisorActor>(actor));

            await registering;
            (await waiting).ShouldBe(actor);
        }

        await system.Terminate();
    }

    [Fact]
    public async Task A_wait_stops_when_the_host_stops()
    {
        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Should.ThrowAsync<OperationCanceledException>(() => RegisteredActor.WaitAsync<MatchSupervisorActor>(new ActorRegistry(), stopping.Token));
    }
}
