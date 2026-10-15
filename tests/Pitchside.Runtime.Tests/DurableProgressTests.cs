using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Tests;

public sealed class DurableProgressTests
{
    [Fact]
    public async Task A_wait_ends_when_the_store_catches_up_with_the_live_state()
    {
        var progress = new DurableProgress();
        progress.Advance("MER-7", 10);

        var waiting = progress.WaitForAsync("MER-7", 12, TimeSpan.FromSeconds(5), CancellationToken.None);
        await Task.Delay(100);
        waiting.IsCompleted.ShouldBeFalse();
        progress.Advance("MER-7", 12);

        (await waiting).ShouldBeTrue();
    }

    [Fact]
    public async Task A_wait_gives_up_when_the_store_does_not_catch_up_in_time()
    {
        var progress = new DurableProgress();
        progress.Advance("MER-7", 10);

        (await progress.WaitForAsync("MER-7", 12, TimeSpan.FromMilliseconds(100), CancellationToken.None)).ShouldBeFalse();
        (await progress.WaitForAsync("MER-7", 10, TimeSpan.Zero, CancellationToken.None)).ShouldBeTrue();
    }
}
