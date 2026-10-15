namespace Pitchside.Api.Broadcast;

/// <summary>Runs a store write or a push again after a brief failure, waiting longer each time, then gives up and throws.</summary>
public static class WriteRetry
{
    /// <summary>The waits between attempts: about nine seconds in all before a write is given up.</summary>
    public static readonly IReadOnlyList<TimeSpan> Delays =
    [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
    ];

    public static async Task RunAsync(Func<CancellationToken, Task> write, IReadOnlyList<TimeSpan> delays, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(delays);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await write(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception) when (attempt < delays.Count && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(delays[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
