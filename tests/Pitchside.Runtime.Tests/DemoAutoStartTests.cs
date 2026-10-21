using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pitchside.Contracts;
using Pitchside.Runtime.Replay;

namespace Pitchside.Runtime.Tests;

/// <summary>The demo seed starts with the host and, with <c>Demo:Loop</c>, again under the next loop id when it ends.</summary>
public sealed class DemoAutoStartTests
{
    [Fact]
    public async Task Each_loop_starts_under_the_next_id_once_the_last_one_has_ended()
    {
        var replays = new ScriptedReplays(new HashSet<int>(), runningChecks: 0);

        await Run(replays, new DemoOptions { AutoStart = true, Seed = 26, Loop = true, LoopPauseSeconds = 0, MaxLoops = 3 });

        replays.Started.ShouldBe(["MER-26", "MER-26-2", "MER-26-3"]);
    }

    [Fact]
    public async Task A_loop_already_stored_is_skipped_after_a_restart()
    {
        var replays = new ScriptedReplays(new HashSet<int> { 1, 2 }, runningChecks: 0);

        await Run(replays, new DemoOptions { AutoStart = true, Seed = 26, Loop = true, LoopPauseSeconds = 0, MaxLoops = 4 });

        replays.Attempts.ShouldBe([1, 2, 3, 4]);
        replays.Started.ShouldBe(["MER-26-3", "MER-26-4"]);
    }

    [Fact]
    public async Task The_next_loop_waits_while_the_last_one_is_still_playing()
    {
        var replays = new ScriptedReplays(new HashSet<int>(), runningChecks: int.MaxValue);
        using var service = Service(replays, new DemoOptions { AutoStart = true, Seed = 26, Loop = true, LoopPauseSeconds = 0, MaxLoops = 3 });

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await service.StopAsync(CancellationToken.None);

        replays.Started.ShouldBe(["MER-26"]);
    }

    [Fact]
    public async Task The_next_loop_waits_until_the_last_match_is_stored_as_ended_not_only_published()
    {
        // The publisher has finished (not running), but ingestion has not yet stored the match as completed.
        var replays = new ScriptedReplays(new HashSet<int>(), runningChecks: 0, storedAs: MatchStatus.Live);
        using var service = Service(replays, new DemoOptions { AutoStart = true, Seed = 26, Loop = true, LoopPauseSeconds = 0, MaxLoops = 3 });

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await service.StopAsync(CancellationToken.None);

        replays.Started.ShouldBe(["MER-26"]);
    }

    [Fact]
    public async Task Without_the_loop_the_seed_is_started_once_and_a_stored_match_is_left_alone()
    {
        var once = new ScriptedReplays(new HashSet<int>(), runningChecks: 0);
        await Run(once, new DemoOptions { AutoStart = true, Seed = 26, Loop = false, LoopPauseSeconds = 0 });
        once.Started.ShouldBe(["MER-26"]);

        var stored = new ScriptedReplays(new HashSet<int> { 1 }, runningChecks: 0);
        await Run(stored, new DemoOptions { AutoStart = true, Seed = 26, Loop = false, LoopPauseSeconds = 0 });
        stored.Attempts.ShouldBe([1]);
        stored.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task Nothing_starts_without_auto_start()
    {
        var replays = new ScriptedReplays(new HashSet<int>(), runningChecks: 0);

        await Run(replays, new DemoOptions { AutoStart = false });

        replays.Attempts.ShouldBeEmpty();
    }

    private static async Task Run(ScriptedReplays replays, DemoOptions options)
    {
        using var service = Service(replays, options);
        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static DemoAutoStart Service(ScriptedReplays replays, DemoOptions options) =>
        new(replays, replays.Store, Options.Create(options), new StartedLifetime(), TimeProvider.System, NullLogger<DemoAutoStart>.Instance);
}
