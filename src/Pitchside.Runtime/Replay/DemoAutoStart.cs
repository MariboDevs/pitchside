using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pitchside.Runtime.Replay;

/// <summary>
/// Starts the demo seed once the host is up, when <c>Demo:AutoStart</c> is on. A match already stored (the host
/// restarted after the demo began) is left as it is: a match id is replayed once per store.
/// </summary>
public sealed partial class DemoAutoStart(
    IReplayService replays,
    IOptions<DemoOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<DemoAutoStart> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var demo = options.Value;
        if (!demo.AutoStart)
        {
            return;
        }

        // The replay publishes onto the bus as soon as it starts, so it waits until every hosted service is running.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
        try
        {
            var replay = await replays.StartAsync(demo.Seed, demo.Speed, stoppingToken).ConfigureAwait(false);
            LogStarted(replay.MatchId, demo.Speed);
        }
        catch (ReplayConflictException)
        {
            LogAlreadyStored(demo.Seed);
        }
        catch (ReplayLimitReachedException)
        {
            LogNoSlot(demo.Seed);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo {MatchId} started at speed {Speed}")]
    private partial void LogStarted(string matchId, double speed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo seed {Seed} was not started: every replay slot is taken")]
    private partial void LogNoSlot(long seed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo seed {Seed} is already stored, so it is not started again")]
    private partial void LogAlreadyStored(long seed);
}
