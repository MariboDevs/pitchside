using Akka.Actor;
using Akka.TestKit.Xunit2;
using Pitchside.Contracts;
using Pitchside.Moments;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.MomentSink;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Tests;

public sealed class MatchActorTests : TestKit
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private static readonly string MatchId = Sample.Document.MatchId;
    private readonly InMemoryMatchStore _store = new();
    private readonly RecordingBroadcaster _broadcaster = new();
    private readonly ChannelMomentSink _sink = new();
    private readonly DurableProgress _progress = new();

    private MatchOutbox Outbox(IMatchStore? store = null) => new(MatchId, store ?? _store, _progress, Sys.Log);

    private async Task<IActorRef> NewActorAsync()
    {
        await _store.SaveMatchAsync(Sample.Stored, CancellationToken.None);
        return Sys.ActorOf(MatchActor.Props(MatchId, _store, _broadcaster, _sink, Outbox()));
    }

    private static Task<MatchStateView> StateAsync(IActorRef actor) => actor.Ask<MatchStateView>(new GetState(MatchId), Wait);

    private static void Ingest(IActorRef actor, params int[] seqs)
    {
        foreach (var seq in seqs)
        {
            actor.Tell(new IngestEvent(Sample.Events[seq - 1]));
        }
    }

    private async Task<int> StoredEventsAsync(int toSeq = int.MaxValue) =>
        (await _store.GetEventsAsync(MatchId, 1, toSeq, CancellationToken.None)).Count;

    [Fact]
    public async Task Events_are_applied_in_sequence()
    {
        var actor = await NewActorAsync();
        Ingest(actor, [.. Enumerable.Range(1, 200)]);

        var state = await StateAsync(actor);
        state.LastSeq.ShouldBe(200);
        state.Status.ShouldBe(MatchStatus.Live);
        state.DroppedEvents.ShouldBe(0);
    }

    [Fact]
    public async Task A_repeated_or_older_event_is_dropped_and_counted()
    {
        var actor = await NewActorAsync();
        Ingest(actor, 1, 2, 3, 2, 1, 3);

        var state = await StateAsync(actor);
        state.LastSeq.ShouldBe(3);
        state.DroppedEvents.ShouldBe(3);
    }

    [Fact]
    public async Task A_gap_waits_for_the_missing_event_then_applies_in_order()
    {
        var actor = await NewActorAsync();
        Ingest(actor, 1, 2, 5, 4);
        (await StateAsync(actor)).LastSeq.ShouldBe(2);

        Ingest(actor, 3);
        (await StateAsync(actor)).LastSeq.ShouldBe(5);
    }

    [Fact]
    public async Task A_gap_is_filled_from_the_store_when_it_holds_the_missing_events()
    {
        var actor = await NewActorAsync();
        Ingest(actor, 1, 2);
        await AwaitAssertAsync(async () => (await StoredEventsAsync(2)).ShouldBe(2), Wait);
        await _store.AppendEventAsync(Sample.Events[2], CancellationToken.None);
        await _store.AppendEventAsync(Sample.Events[3], CancellationToken.None);

        Ingest(actor, 5);

        await AwaitAssertAsync(async () => (await StateAsync(actor)).LastSeq.ShouldBe(5), Wait);
    }

    [Fact]
    public async Task A_second_gap_behind_a_resend_is_requested_too()
    {
        var actor = await NewActorAsync();
        Ingest(actor, 1);
        await AwaitAssertAsync(async () => (await StoredEventsAsync(1)).ShouldBe(1), Wait);
        foreach (var seq in new[] { 2, 3, 4, 6, 7 })
        {
            await _store.AppendEventAsync(Sample.Events[seq - 1], CancellationToken.None);
        }

        Ingest(actor, 5, 8);

        await AwaitAssertAsync(async () => (await StateAsync(actor)).LastSeq.ShouldBe(8), Wait);
    }

    [Fact]
    public async Task A_restart_rebuilds_the_state_from_the_store()
    {
        var supervisor = Sys.ActorOf(Props.Create(() => new MatchSupervisorActor(_store, _broadcaster, _sink, new UnusedReplays(), _progress)), "matches");
        await _store.SaveMatchAsync(Sample.Stored, CancellationToken.None);
        foreach (var e in Sample.Events.Take(400))
        {
            supervisor.Tell(new IngestEvent(e));
        }

        await AwaitAssertAsync(async () => (await StoredEventsAsync(400)).ShouldBe(400), Wait);
        var before = await supervisor.Ask<List<Moment>>(new GetMoments(MatchId), Wait);

        var child = await Sys.ActorSelection($"/user/matches/{MatchSupervisorActor.ChildName(MatchId)}").ResolveOne(Wait);
        child.Tell(Kill.Instance);

        await AwaitAssertAsync(async () => (await supervisor.Ask<MatchStateView>(new GetState(MatchId), Wait)).LastSeq.ShouldBe(400), Wait);
        var after = await supervisor.Ask<List<Moment>>(new GetMoments(MatchId), Wait);
        after.Select(m => m.Id).ShouldBe(before.Select(m => m.Id));
        after.Select(m => m.Id).ShouldBe(MatchMoments.Detect(Sample.Document, Sample.Events.Take(400)).Select(m => m.Id));

        // Ingest continues where the stored events end.
        supervisor.Tell(new IngestEvent(Sample.Events[400]));
        await AwaitAssertAsync(async () => (await supervisor.Ask<MatchStateView>(new GetState(MatchId), Wait)).LastSeq.ShouldBe(401), Wait);
    }

    [Fact]
    public async Task Pushes_arrive_in_order_and_moments_reach_the_sink()
    {
        var actor = await NewActorAsync();
        Ingest(actor, [.. Enumerable.Range(1, Sample.Events.Count)]);

        await AwaitAssertAsync(async () => (await StateAsync(actor)).Status.ShouldBe(MatchStatus.Completed), Wait);
        await AwaitAssertAsync(() => _broadcaster.Pushes.Count(p => p.Kind == "event").ShouldBe(Sample.Events.Count), Wait);

        var lastEvent = 0;
        foreach (var (kind, seq) in _broadcaster.Pushes)
        {
            if (kind == "event")
            {
                seq.ShouldBe(lastEvent + 1);
                lastEvent = seq;
            }
            else if (kind is "facts" or "moment")
            {
                seq.ShouldBe(lastEvent, $"{kind} {seq} must follow its event");
            }
        }

        var expected = MatchMoments.Detect(Sample.Document, Sample.Events);
        _broadcaster.Pushes.Count(p => p.Kind == "moment").ShouldBe(expected.Count);
        _sink.Complete();
        var queued = new List<string>();
        await foreach (var item in _sink.ReadAllAsync(CancellationToken.None))
        {
            queued.Add(item.Moment.Id);
        }

        queued.ShouldBe(expected.Select(m => m.Id));
    }

    [Fact]
    public async Task Moments_fact_sheets_and_status_are_stored()
    {
        var actor = await NewActorAsync();
        Ingest(actor, [.. Enumerable.Range(1, Sample.Events.Count)]);

        var expected = MatchMoments.Detect(Sample.Document, Sample.Events);
        await AwaitAssertAsync(async () => (await _store.GetMomentsAsync(MatchId, CancellationToken.None)).Count.ShouldBe(expected.Count), Wait);
        foreach (var moment in expected)
        {
            (await _store.GetFactSheetAsync(MatchId, moment.FactSheetSeq, CancellationToken.None))!.AsOfSeq.ShouldBe(moment.FactSheetSeq);
        }

        await AwaitAssertAsync(async () => (await _store.GetMatchAsync(MatchId, CancellationToken.None))!.Status.ShouldBe(MatchStatus.Completed), Wait);
        await AwaitAssertAsync(async () => (await StoredEventsAsync()).ShouldBe(Sample.Events.Count), Wait);
    }

    [Fact]
    public async Task An_actor_waits_for_its_document_before_ingesting()
    {
        var actor = Sys.ActorOf(MatchActor.Props(MatchId, _store, _broadcaster, _sink, Outbox()));
        Ingest(actor, 1, 2, 3);
        await _store.SaveMatchAsync(Sample.Stored, CancellationToken.None);

        await AwaitAssertAsync(async () => (await StateAsync(actor)).LastSeq.ShouldBe(3), Wait);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failed_append_is_retried_in_order_and_the_store_stays_gapless(bool timeOut)
    {
        var flaky = new FlakyAppendStore(_store, new Dictionary<int, int> { [3] = 2 }, timeOut);
        await _store.SaveMatchAsync(Sample.Stored, CancellationToken.None);
        var actor = Sys.ActorOf(MatchActor.Props(MatchId, flaky, _broadcaster, _sink, Outbox(flaky)));

        Ingest(actor, 1, 2, 3, 4, 5);

        await AwaitAssertAsync(async () => (await StoredEventsAsync()).ShouldBe(5), Wait);
        flaky.Failures.ShouldBe(2);
        (await _store.GetEventsAsync(MatchId, 1, 5, CancellationToken.None)).Select(e => e.Seq).ShouldBe([1, 2, 3, 4, 5]);
        _progress.For(MatchId).ShouldBe(5);
    }

    [Fact]
    public async Task Pause_resume_and_stop_reach_the_actor_and_a_stop_outlasts_full_time()
    {
        var actor = await NewActorAsync();
        Ingest(actor, 1, 2);
        await AwaitAssertAsync(async () => (await StateAsync(actor)).Status.ShouldBe(MatchStatus.Live), Wait);

        actor.Tell(new SetLifecycle(MatchId, MatchStatus.Paused));
        await AwaitAssertAsync(async () => (await StateAsync(actor)).Status.ShouldBe(MatchStatus.Paused), Wait);
        actor.Tell(new SetLifecycle(MatchId, MatchStatus.Live));
        actor.Tell(new SetLifecycle(MatchId, MatchStatus.Stopped));
        Ingest(actor, [.. Enumerable.Range(3, Sample.Events.Count - 2)]);

        await AwaitAssertAsync(async () => (await StateAsync(actor)).LastSeq.ShouldBe(Sample.Events.Count), Wait);
        (await StateAsync(actor)).Status.ShouldBe(MatchStatus.Stopped);
        await AwaitAssertAsync(async () => (await _store.GetMatchAsync(MatchId, CancellationToken.None))!.Status.ShouldBe(MatchStatus.Stopped), Wait);
        _broadcaster.Statuses.ShouldContain(s => s.Status == MatchStatus.Paused);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("MER 7")]
    [InlineData("")]
    public async Task The_supervisor_refuses_an_invalid_match_id(string matchId)
    {
        var supervisor = Sys.ActorOf(Props.Create(() => new MatchSupervisorActor(_store, _broadcaster, _sink, new UnusedReplays(), _progress)));
        await Should.ThrowAsync<ArgumentException>(() => supervisor.Ask<MatchStateView>(new GetState(matchId), Wait));
    }

    [Fact]
    public async Task Unsubscribing_from_an_unknown_match_creates_no_actor()
    {
        var supervisor = Sys.ActorOf(Props.Create(() => new MatchSupervisorActor(_store, _broadcaster, _sink, new UnusedReplays(), _progress)), "quiet");
        supervisor.Tell(new Unsubscribe("MER-404", "connection-1"));
        supervisor.Tell(new SetLifecycle("MER-404", MatchStatus.Stopped));

        await Task.Delay(200);
        await Should.ThrowAsync<ActorNotFoundException>(() => Sys.ActorSelection($"/user/quiet/{MatchSupervisorActor.ChildName("MER-404")}").ResolveOne(TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task A_rebuild_rewrites_what_a_crash_left_unwritten_and_requeues_unnarrated_moments()
    {
        // Every event is stored, but the crash came before any fact sheet, moment, status or narrative was written.
        await _store.SaveMatchAsync(Sample.Stored, CancellationToken.None);
        foreach (var e in Sample.Events)
        {
            await _store.AppendEventAsync(e, CancellationToken.None);
        }

        var expected = MatchMoments.Detect(Sample.Document, Sample.Events);
        var actor = Sys.ActorOf(MatchActor.Props(MatchId, _store, _broadcaster, _sink, Outbox()));

        await AwaitAssertAsync(async () => (await StateAsync(actor)).LastSeq.ShouldBe(Sample.Events.Count), Wait);
        await AwaitAssertAsync(async () => (await _store.GetMatchAsync(MatchId, CancellationToken.None))!.Status.ShouldBe(MatchStatus.Completed), Wait);
        await AwaitAssertAsync(async () => (await _store.GetMomentsAsync(MatchId, CancellationToken.None)).Count.ShouldBe(expected.Count), Wait);
        _progress.For(MatchId).ShouldBe(Sample.Events.Count);

        var requeued = new List<string>();
        using var read = new CancellationTokenSource(Wait);
        await foreach (var item in _sink.ReadAllAsync(read.Token))
        {
            requeued.Add(item.Moment.Id);
            if (requeued.Count == expected.Count)
            {
                break;
            }
        }

        requeued.ShouldBe(expected.Select(m => m.Id));
        _broadcaster.Pushes.ShouldNotContain(p => p.Kind == "event" || p.Kind == "moment");
    }

    [Fact]
    public async Task Events_buffered_behind_a_gap_survive_a_restart()
    {
        await _store.SaveMatchAsync(Sample.Stored, CancellationToken.None);
        var outbox = Outbox();
        var first = Sys.ActorOf(MatchActor.Props(MatchId, _store, _broadcaster, _sink, outbox));
        Ingest(first, 1, 3);
        await AwaitAssertAsync(async () => (await StateAsync(first)).LastSeq.ShouldBe(1), Wait);
        await AwaitAssertAsync(() => outbox.Pending.Keys.ShouldBe([3]), Wait);

        await first.GracefulStop(Wait);
        var second = Sys.ActorOf(MatchActor.Props(MatchId, _store, _broadcaster, _sink, outbox));
        Ingest(second, 2);

        await AwaitAssertAsync(async () => (await StateAsync(second)).LastSeq.ShouldBe(3), Wait);
        await AwaitAssertAsync(async () => (await StoredEventsAsync()).ShouldBe(3), Wait);
    }
}
