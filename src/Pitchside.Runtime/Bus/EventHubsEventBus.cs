using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Azure.Core;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Pitchside.Contracts;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Bus;

/// <summary>
/// Azure Event Hubs: a producer that partitions by match id, and an <see cref="EventProcessorClient"/> with blob
/// checkpoints for each consumer group. Authentication is a token credential; a connection string is never read.
/// A delivered event is checkpointed only once <see cref="DurableProgress"/> shows the match actor has stored it, so a
/// restart replays anything not yet durable (the actor drops the duplicates) and never skips an event.
/// </summary>
public sealed partial class EventHubsEventBus : IEventBus, IAsyncDisposable
{
    private readonly EventHubsBusOptions _options;
    private readonly TokenCredential _credential;
    private readonly DurableProgress _progress;
    private readonly ILogger<EventHubsEventBus> _logger;
    private readonly EventHubProducerClient _producer;

    public EventHubsEventBus(EventHubsBusOptions options, TokenCredential credential, DurableProgress progress, ILogger<EventHubsEventBus> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        _options = options;
        _credential = credential;
        _progress = progress;
        _logger = logger;
        _producer = new EventHubProducerClient(Require(options.Namespace, "Bus:EventHubs:Namespace"), Require(options.HubName, "Bus:EventHubs:HubName"), credential);
    }

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var data = new EventData(JsonSerializer.SerializeToUtf8Bytes(envelope, ContractJson.Options));
        await _producer.SendAsync([data], new SendEventOptions { PartitionKey = envelope.MatchId }, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<EventEnvelope> SubscribeAsync(string consumerGroup, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var checkpoints = new BlobContainerClient(_options.CheckpointBlobUri ?? throw new InvalidOperationException("Bus:EventHubs:CheckpointBlobUri is not configured."), _credential);
        var processor = new EventProcessorClient(
            checkpoints,
            consumerGroup == BusOptions.IngestConsumerGroup ? EventHubConsumerClient.DefaultConsumerGroupName : consumerGroup,
            Require(_options.Namespace, "Bus:EventHubs:Namespace"),
            Require(_options.HubName, "Bus:EventHubs:HubName"),
            _credential);
        var received = Channel.CreateBounded<(EventEnvelope Envelope, ProcessEventArgs Args)>(InProcessEventBus.Capacity);

        processor.ProcessEventAsync += async args =>
        {
            if (args.HasEvent)
            {
                var envelope = JsonSerializer.Deserialize<EventEnvelope>(args.Data.EventBody.ToArray(), ContractJson.Options)!;
                await received.Writer.WriteAsync((envelope, args), args.CancellationToken).ConfigureAwait(false);
            }
        };
        processor.ProcessErrorAsync += args =>
        {
            LogProcessorError(args.Exception, args.PartitionId ?? "none", args.Operation ?? "unknown");
            return Task.CompletedTask;
        };

        // Per partition, the delivered events not yet checkpointed, oldest first.
        var pending = new Dictionary<string, Queue<(EventEnvelope Envelope, ProcessEventArgs Args)>>(StringComparer.Ordinal);
        await processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (var (envelope, args) in received.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return envelope;
                if (!pending.TryGetValue(args.Partition.PartitionId, out var queue))
                {
                    queue = new();
                    pending[args.Partition.PartitionId] = queue;
                }

                queue.Enqueue((envelope, args));
                await CheckpointDurableAsync(queue).ConfigureAwait(false);
            }
        }
        finally
        {
            await processor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync() => _producer.DisposeAsync();

    /// <summary>Checkpoints the newest event of a partition's queue whose store write has landed, and drops it and older ones.</summary>
    private async Task CheckpointDurableAsync(Queue<(EventEnvelope Envelope, ProcessEventArgs Args)> queue)
    {
        ProcessEventArgs? durable = null;
        while (queue.TryPeek(out var head) && head.Envelope.Seq <= _progress.For(head.Envelope.MatchId))
        {
            durable = queue.Dequeue().Args;
        }

        if (durable is { } args)
        {
            await args.UpdateCheckpointAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Event Hubs processor error on partition {PartitionId} during {Operation}")]
    private partial void LogProcessorError(Exception exception, string partitionId, string operation);

    private static string Require(string? value, string key) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{key} is not configured.") : value;
}
