using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Pitchside.Contracts;

namespace Pitchside.Runtime.Bus;

/// <summary>
/// The local bus: one bounded channel (10,000) per consumer group, each group seeing every event once, in publish
/// order. A group's channel exists from its first subscription, or from the first publish if
/// <see cref="BusOptions.IngestConsumerGroup"/>, so ingest never misses an event published before it subscribed.
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
    public const int Capacity = 10_000;

    private readonly ConcurrentDictionary<string, Channel<EventEnvelope>> _groups = new(StringComparer.Ordinal);

    public InProcessEventBus()
    {
        Group(BusOptions.IngestConsumerGroup);
    }

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        foreach (var channel in _groups.Values)
        {
            await channel.Writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>How many published events a consumer group has not read yet.</summary>
    public int Backlog(string consumerGroup) => _groups.TryGetValue(consumerGroup, out var channel) ? channel.Reader.Count : 0;

    public async IAsyncEnumerable<EventEnvelope> SubscribeAsync(string consumerGroup, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var envelope in Group(consumerGroup).Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return envelope;
        }
    }

    private Channel<EventEnvelope> Group(string consumerGroup) => _groups.GetOrAdd(consumerGroup, _ =>
        Channel.CreateBounded<EventEnvelope>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        }));
}
