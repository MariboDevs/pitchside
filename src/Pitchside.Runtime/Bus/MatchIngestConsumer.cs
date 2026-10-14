using Akka.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;

namespace Pitchside.Runtime.Bus;

/// <summary>Reads the bus's ingest consumer group and hands each event to its match actor.</summary>
public sealed partial class MatchIngestConsumer(IEventBus bus, IRequiredActor<MatchSupervisorActor> supervisor, ILogger<MatchIngestConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var matches = await supervisor.GetAsync(stoppingToken).ConfigureAwait(false);
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
