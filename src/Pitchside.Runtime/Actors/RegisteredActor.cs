using Akka.Actor;
using Akka.Hosting;

namespace Pitchside.Runtime.Actors;

/// <summary>
/// Waits for an actor to be registered without Akka.Hosting's <c>ActorRegistry.GetAsync</c>, which can lose the
/// wake-up: when the registration lands between its first look and its waiter being added, the waiter is never
/// completed and the caller waits forever. A service that starts before Akka (the bus's ingest consumer) checks the
/// registry until the actor is there instead.
/// </summary>
public static class RegisteredActor
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    public static async Task<IActorRef> WaitAsync<TKey>(ActorRegistry registry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registry);
        while (true)
        {
            if (registry.TryGet<TKey>(out var actor))
            {
                return actor;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
