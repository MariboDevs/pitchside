using Pitchside.Contracts;

namespace Pitchside.Runtime.Bus;

/// <summary>The live event feed between the producer and the ingest stage. Partitioned by match id, so one match is ordered.</summary>
public interface IEventBus
{
    Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken);

    IAsyncEnumerable<EventEnvelope> SubscribeAsync(string consumerGroup, CancellationToken cancellationToken);
}
