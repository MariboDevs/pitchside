using Akka.Actor;
using Akka.Hosting;

namespace Pitchside.Api.IntegrationTests;

/// <summary>An <see cref="IRequiredActor{TActor}"/> that hands out a given actor in place of the registered one.</summary>
internal sealed class FixedRequiredActor<TActor>(IActorRef actor) : IRequiredActor<TActor>
{
    public IActorRef ActorRef => actor;

    public Task<IActorRef> GetAsync(CancellationToken cancel = default) => Task.FromResult(actor);
}
