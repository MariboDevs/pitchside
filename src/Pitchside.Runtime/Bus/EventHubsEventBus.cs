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
/// restart replays anything not yet durable (the actor drops the duplicates) and never skips an event. Pending events
/// are rechecked every <see cref="CheckpointInterval"/> while a partition is idle and once more before stopping.
/// </summary>
public sealed partial class EventHubsEventBus : IEventBus, IAsyncDisposable
{
    public static readonly TimeSpan CheckpointInterval = TimeSpan.FromSeconds(5);

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
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wake at least every few seconds, so an idle partition's tail is checkpointed once its writes land.
                if (!received.Reader.TryRead(out var item))
                {
                    var arrived = await WaitToReadAsync(received.Reader, CheckpointInterval, cancellationToken).ConfigureAwait(false);
                    if (arrived is null)
                    {
                        break;
                    }

                    if (arrived == false)
                    {
                        await CheckpointAllAsync(pending).ConfigureAwait(false);
                    }

                    continue;
                }

                var (envelope, args) = item;
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
            try
            {
                await CheckpointAllAsync(pending).ConfigureAwait(false);
            }
            finally
            {
                await processor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public ValueTask DisposeAsync() => _producer.DisposeAsync();

    /// <summary>
    /// True when an item is ready, false when the interval passed with none, null when the channel completed. A
    /// cancelled token ends the stream the same way a completed channel does.
    /// </summary>
    private static async Task<bool?> WaitToReadAsync<T>(ChannelReader<T> reader, TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(interval);
        try
        {
            return await reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false) ? true : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task CheckpointAllAsync(Dictionary<string, Queue<(EventEnvelope Envelope, ProcessEventArgs Args)>> pending)
    {
        foreach (var queue in pending.Values)
        {
            await CheckpointDurableAsync(queue).ConfigureAwait(false);
        }
    }

    /// <summary>Checkpoints the newest event of a partition's queue whose store write has landed, and drops it and older ones.</summary>
    /// <remarks>
    /// Entries leave the queue only after the checkpoint succeeds. A failed checkpoint is logged and retried on the next
    /// event or interval, never allowed to end ingestion: at worst a restart redelivers events the actor drops.
    /// </remarks>
    private async Task CheckpointDurableAsync(Queue<(EventEnvelope Envelope, ProcessEventArgs Args)> queue)
    {
        var eligible = queue.TakeWhile(entry => entry.Envelope.Seq <= _progress.For(entry.Envelope.MatchId)).Count();
        if (eligible == 0)
        {
            return;
        }

        var newest = queue.ElementAt(eligible - 1);
        try
        {
            await newest.Args.UpdateCheckpointAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogCheckpointFailed(ex, newest.Args.Partition.PartitionId, newest.Envelope.MatchId, newest.Envelope.Seq);
            return;
        }

        for (var i = 0; i < eligible; i++)
        {
            queue.Dequeue();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Checkpoint on partition {PartitionId} at {MatchId}/{Seq} failed; it is retried")]
    private partial void LogCheckpointFailed(Exception exception, string partitionId, string matchId, int seq);

    [LoggerMessage(Level = LogLevel.Error, Message = "Event Hubs processor error on partition {PartitionId} during {Operation}")]
    private partial void LogProcessorError(Exception exception, string partitionId, string operation);

    private static string Require(string? value, string key) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{key} is not configured.") : value;
}
