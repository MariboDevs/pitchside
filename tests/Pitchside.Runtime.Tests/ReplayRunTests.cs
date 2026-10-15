using Pitchside.Runtime.Replay;

namespace Pitchside.Runtime.Tests;

public sealed class ReplayRunTests
{
    [Fact]
    public void A_stop_that_races_the_runs_end_does_nothing()
    {
        var run = new ReplayRun("MER-7", new CancellationTokenSource());
        var token = run.Cancellation.Token;
        run.Dispose();

        Should.NotThrow(run.Cancel);
        token.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public void A_running_replay_is_cancelled()
    {
        using var run = new ReplayRun("MER-7", new CancellationTokenSource());
        run.Cancel();
        run.Cancellation.IsCancellationRequested.ShouldBeTrue();
    }
}
