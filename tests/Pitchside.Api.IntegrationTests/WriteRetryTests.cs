using Pitchside.Api.Broadcast;

namespace Pitchside.Api.IntegrationTests;

public sealed class WriteRetryTests
{
    private static readonly TimeSpan[] Quick = [TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)];

    [Fact]
    public async Task A_write_that_fails_briefly_is_retried_until_it_lands()
    {
        var attempts = 0;
        await WriteRetry.RunAsync(
            _ => ++attempts < 3 ? throw new InvalidOperationException("store busy") : Task.CompletedTask,
            Quick,
            CancellationToken.None);

        attempts.ShouldBe(3);
    }

    [Fact]
    public async Task A_write_that_keeps_failing_throws_after_the_last_retry()
    {
        var attempts = 0;
        await Should.ThrowAsync<InvalidOperationException>(() => WriteRetry.RunAsync(
            _ =>
            {
                attempts++;
                throw new InvalidOperationException("store down");
            },
            Quick,
            CancellationToken.None));

        attempts.ShouldBe(3);
    }
}
