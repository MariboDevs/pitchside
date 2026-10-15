using System.Diagnostics;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Replay;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Tests;

/// <summary>The whole local runtime in a host: replay, bus, ingest consumer, match actors and the in-memory store.</summary>
public sealed class ReplayServiceTests : IAsyncLifetime
{
    private IHost _host = null!;

    private IReplayService Replays => _host.Services.GetRequiredService<IReplayService>();

    private IMatchStore Store => _host.Services.GetRequiredService<IMatchStore>();

    private async Task<MatchStatus> ActorStatusAsync(string matchId) =>
        (await (await _host.Services.GetRequiredService<IRequiredActor<MatchSupervisorActor>>().GetAsync())
            .Ask<MatchStateView>(new GetState(matchId), TimeSpan.FromSeconds(5))).Status;

    public async Task InitializeAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Replay:MaxConcurrent"] = "2" });
        builder.Services.AddPitchsideRuntime(builder.Configuration);
        _host = builder.Build();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task Speed_zero_replays_the_sample_in_under_five_seconds_and_stores_every_event()
    {
        var stopwatch = Stopwatch.StartNew();
        var started = await Replays.StartAsync(7, 0, CancellationToken.None);
        started.MatchId.ShouldBe("MER-7");
        started.Events.ShouldBe(Sample.Events.Count);

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5)
               && (await Store.GetEventsAsync("MER-7", 1, int.MaxValue, CancellationToken.None)).Count < Sample.Events.Count)
        {
            await Task.Delay(50);
        }

        stopwatch.Stop();
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        (await Store.GetEventsAsync("MER-7", 1, int.MaxValue, CancellationToken.None)).Select(e => e.Id).ShouldBe(Sample.Events.Select(e => e.Id));
        await WaitFor(async () => (await Store.GetMatchAsync("MER-7", CancellationToken.None))!.Status == MatchStatus.Completed);
        await WaitFor(() => Task.FromResult(Replays.Running.Count == 0));
        Replays.IsInstant("MER-7").ShouldBeTrue();
    }

    [Fact]
    public async Task The_cap_refuses_a_replay_beyond_the_limit()
    {
        await Replays.StartAsync(101, 0.001, CancellationToken.None);
        await Replays.StartAsync(102, 0.001, CancellationToken.None);

        await Should.ThrowAsync<ReplayLimitReachedException>(() => Replays.StartAsync(103, 0.001, CancellationToken.None));
        Replays.Running.Count.ShouldBe(2);
        Replays.IsInstant("MER-101").ShouldBeFalse();
    }

    [Fact]
    public async Task A_match_is_replayed_once()
    {
        await Replays.StartAsync(104, 0.001, CancellationToken.None);
        await Should.ThrowAsync<ReplayConflictException>(() => Replays.StartAsync(104, 0.001, CancellationToken.None));
    }

    [Fact]
    public async Task Stop_ends_the_replay_and_marks_the_match_stopped()
    {
        var started = await Replays.StartAsync(105, 0.001, CancellationToken.None);
        Replays.Stop(started.MatchId).ShouldBeTrue();

        await WaitFor(() => Task.FromResult(!Replays.Running.Contains(started.MatchId)));
        await WaitFor(async () => (await Store.GetMatchAsync(started.MatchId, CancellationToken.None))!.Status == MatchStatus.Stopped);
        await WaitFor(async () => await ActorStatusAsync(started.MatchId) == MatchStatus.Stopped);
        Replays.Stop("MER-404").ShouldBeFalse();
    }

    [Fact]
    public async Task Pause_holds_the_replay_and_resume_continues_it()
    {
        var started = await Replays.StartAsync(106, 50, CancellationToken.None);
        await WaitFor(async () => (await Store.GetEventsAsync(started.MatchId, 1, 5, CancellationToken.None)).Count == 5);
        Replays.Pause(started.MatchId).ShouldBeTrue();
        await WaitFor(async () => await ActorStatusAsync(started.MatchId) == MatchStatus.Paused);
        await WaitFor(async () => (await Store.GetMatchAsync(started.MatchId, CancellationToken.None))!.Status == MatchStatus.Paused);
        await Task.Delay(300);
        var paused = (await Store.GetEventsAsync(started.MatchId, 1, int.MaxValue, CancellationToken.None)).Count;
        await Task.Delay(500);
        (await Store.GetEventsAsync(started.MatchId, 1, int.MaxValue, CancellationToken.None)).Count.ShouldBe(paused);

        Replays.Resume(started.MatchId).ShouldBeTrue();
        await WaitFor(async () => (await Store.GetEventsAsync(started.MatchId, 1, int.MaxValue, CancellationToken.None)).Count > paused);
        (await ActorStatusAsync(started.MatchId)).ShouldBe(MatchStatus.Live);
        Replays.Stop(started.MatchId);
    }

    [Fact]
    public async Task A_start_that_cannot_store_its_match_frees_its_slot()
    {
        var existing = Sample.Stored with { Document = Sample.Stored.Document with { MatchId = "MER-107" } };
        await Store.SaveMatchAsync(existing, CancellationToken.None);

        await Should.ThrowAsync<ReplayConflictException>(() => Replays.StartAsync(107, 0, CancellationToken.None));
        Replays.Running.ShouldNotContain("MER-107");
        (await Store.GetMatchAsync("MER-107", CancellationToken.None))!.Document.ShouldBe(existing.Document);

        var first = await Replays.StartAsync(108, 0.001, CancellationToken.None);
        var second = await Replays.StartAsync(109, 0.001, CancellationToken.None);
        Replays.Running.ShouldBe([first.MatchId, second.MatchId], ignoreOrder: true);
        Replays.Stop(first.MatchId);
        Replays.Stop(second.MatchId);
    }

    private static async Task WaitFor(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "condition not met within 10 s");
            await Task.Delay(50);
        }
    }
}
