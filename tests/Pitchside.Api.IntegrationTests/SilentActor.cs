using Akka.Actor;

namespace Pitchside.Api.IntegrationTests;

/// <summary>An actor that takes every message and answers none, as a match actor deep in a replay's queue would.</summary>
internal sealed class SilentActor : ReceiveActor
{
    public SilentActor() => ReceiveAny(_ => { });
}
