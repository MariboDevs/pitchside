using Microsoft.Extensions.Hosting;

namespace Pitchside.Runtime.Tests;

/// <summary>A host lifetime that has already started, for hosted services that wait for it.</summary>
internal sealed class StartedLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted { get; } = new(canceled: true);

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication()
    {
    }
}
