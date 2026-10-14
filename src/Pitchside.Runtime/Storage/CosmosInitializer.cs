using Microsoft.Extensions.Hosting;

namespace Pitchside.Runtime.Storage;

/// <summary>Creates the Cosmos DB database and containers at startup, before anything reads or writes.</summary>
public sealed class CosmosInitializer(CosmosMatchStore store) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => store.EnsureCreatedAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
