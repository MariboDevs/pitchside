using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pitchside.Engine;

namespace Pitchside.Runtime.Replay;

/// <summary>
/// Starts the demo seed once the host is up, when <c>Demo:AutoStart</c> is on, and with <c>Demo:Loop</c> starts it again
/// under the next loop id (<c>MER-26-2</c>, <c>MER-26-3</c>) a pause after each replay ends, however it ended. A loop id
/// already stored (the host restarted part way through the demo) is skipped, because a match id is replayed once per
/// store. Without <c>Demo:Loop</c> the seed is started once, and a stored match is left as it is.
/// </summary>
public sealed partial class DemoAutoStart(
    IReplayService replays,
    IOptions<DemoOptions> options,
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<DemoAutoStart> logger) : BackgroundService
{
    /// <summary>How often a running demo replay is checked for having ended.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

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

        var pause = TimeSpan.FromSeconds(Math.Max(0, demo.LoopPauseSeconds));
        var loop = MatchLoops.First;
        while (!stoppingToken.IsCancellationRequested && loop <= Math.Max(MatchLoops.First, demo.MaxLoops))
        {
            ReplayStarted replay;
            try
            {
                replay = await replays.StartAsync(demo.Seed, demo.Speed, loop, stoppingToken).ConfigureAwait(false);
            }
            catch (ReplayConflictException)
            {
                LogAlreadyStored(MatchLoops.MatchIdFor(demo.Seed, loop));
                if (!demo.Loop)
                {
                    return;
                }

                loop++;
                continue;
            }
            catch (ReplayLimitReachedException)
            {
                LogNoSlot(demo.Seed);
                if (!demo.Loop)
                {
                    return;
                }

                await Task.Delay(pause > PollInterval ? pause : PollInterval, time, stoppingToken).ConfigureAwait(false);
                continue;
            }

            LogStarted(replay.MatchId, demo.Speed);
            if (!demo.Loop)
            {
                return;
            }

            while (replays.Running.Contains(replay.MatchId))
            {
                await Task.Delay(PollInterval, time, stoppingToken).ConfigureAwait(false);
            }

            LogEnded(replay.MatchId, pause.TotalSeconds);
            await Task.Delay(pause, time, stoppingToken).ConfigureAwait(false);
            loop++;
        }

        if (!stoppingToken.IsCancellationRequested)
        {
            LogLoopsExhausted(demo.Seed, demo.MaxLoops);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo {MatchId} started at speed {Speed}")]
    private partial void LogStarted(string matchId, double speed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo {MatchId} ended; the next loop starts in {PauseSeconds} s")]
    private partial void LogEnded(string matchId, double pauseSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo seed {Seed} was not started: every replay slot is taken")]
    private partial void LogNoSlot(long seed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo {MatchId} is already stored, so it is not started again")]
    private partial void LogAlreadyStored(string matchId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo seed {Seed} has used all {MaxLoops} loops; it is not started again")]
    private partial void LogLoopsExhausted(long seed, int maxLoops);
}
