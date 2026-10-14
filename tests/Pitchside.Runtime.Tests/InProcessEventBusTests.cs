using Pitchside.Contracts;
using Pitchside.Runtime.Bus;

namespace Pitchside.Runtime.Tests;

public sealed class InProcessEventBusTests
{
    private static EventEnvelope Envelope(int seq) => EnvelopeFactory.Wrap(Sample.Events[seq - 1], DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Ingest_receives_events_published_before_it_subscribed_in_order()
    {
        var bus = new InProcessEventBus();
        for (var seq = 1; seq <= 50; seq++)
        {
            await bus.PublishAsync(Envelope(seq), CancellationToken.None);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = await bus.SubscribeAsync(BusOptions.IngestConsumerGroup, cts.Token).Take(50).ToListAsync(cts.Token);
        received.Select(e => e.Seq).ShouldBe(Enumerable.Range(1, 50));
    }

    [Fact]
    public async Task Each_consumer_group_sees_every_event()
    {
        var bus = new InProcessEventBus();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var other = bus.SubscribeAsync("analytics", cts.Token).Take(10).ToListAsync(cts.Token);
        for (var seq = 1; seq <= 10; seq++)
        {
            await bus.PublishAsync(Envelope(seq), CancellationToken.None);
        }

        var ingest = await bus.SubscribeAsync(BusOptions.IngestConsumerGroup, cts.Token).Take(10).ToListAsync(cts.Token);
        ingest.Select(e => e.Seq).ShouldBe(Enumerable.Range(1, 10));
        (await other).Select(e => e.Seq).ShouldBe(Enumerable.Range(1, 10));
    }

    [Fact]
    public void An_envelope_round_trips_its_event()
    {
        var envelope = Envelope(1);
        envelope.MatchId.ShouldBe("MER-7");
        envelope.Seq.ShouldBe(1);
        envelope.Type.ShouldBe("kick_off");
        EnvelopeFactory.Unwrap(envelope).ShouldBe(Sample.Events[0]);
    }
}
