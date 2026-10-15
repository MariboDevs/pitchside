using Akka.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;

namespace Pitchside.Runtime.Bus;

/// <summary>
/// Reads the bus's ingest consumer group and hands each event to its match actor. It starts before the actor system
/// has registered the supervisor, so it waits for it with <see cref="RegisteredActor"/> rather than
/// <c>IRequiredActor.GetAsync</c>, whose lost wake-up left this consumer waiting forever and every replay unread.
/// </summary>
public sealed partial class MatchIngestConsumer(IEventBus bus, ActorRegistry registry, ILogger<MatchIngestConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var matches = await RegisteredActor.WaitAsync<MatchSupervisorActor>(registry, stoppingToken).ConfigureAwait(false);
        await foreach (var envelope in bus.SubscribeAsync(BusOptions.IngestConsumerGroup, stoppingToken).ConfigureAwait(false))
        {
            try
            {
                matches.Tell(new IngestEvent(EnvelopeFactory.Unwrap(envelope)), Akka.Actor.ActorRefs.NoSender);
            }
            catch (System.Text.Json.JsonException ex)
            {
                LogUnreadable(ex, envelope.MatchId, envelope.Seq);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Envelope {MatchId}/{Seq} could not be read and was skipped")]
    private partial void LogUnreadable(Exception exception, string matchId, int seq);
}
