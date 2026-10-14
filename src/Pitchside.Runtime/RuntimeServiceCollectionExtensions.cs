using Akka.Hosting;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Broadcast;
using Pitchside.Runtime.Bus;
using Pitchside.Runtime.MomentSink;
using Pitchside.Runtime.Replay;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime;

/// <summary>
/// Registers the live runtime. Every external dependency has an in-process local mode, chosen by configuration:
/// <c>Storage:Provider</c> (<c>InMemory</c> or <c>Cosmos</c>) and <c>Bus:Provider</c> (<c>InProcess</c> or
/// <c>EventHubs</c>). Azure is reached only with a token credential.
/// </summary>
public static class RuntimeServiceCollectionExtensions
{
    public const string ActorSystemName = "pitchside";

    /// <summary>The Aspire connection string name of the local Cosmos DB emulator.</summary>
    public const string CosmosEmulatorConnectionName = "cosmos";

    public static IServiceCollection AddPitchsideRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<BusOptions>(configuration.GetSection(BusOptions.SectionName));
        services.Configure<ReplayOptions>(configuration.GetSection(ReplayOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.TryAddSingleton<IMatchBroadcaster, NullMatchBroadcaster>();
        services.TryAddSingleton<IMomentSink, ChannelMomentSink>();
        services.TryAddSingleton<DurableProgress>();

        AddStorage(services, configuration);
        AddBus(services, configuration);

        services.TryAddSingleton<ReplayService>();
        services.TryAddSingleton<IReplayService>(sp => sp.GetRequiredService<ReplayService>());
        services.AddHostedService(sp => sp.GetRequiredService<ReplayService>());
        services.AddHostedService<MatchIngestConsumer>();

        services.AddAkka(ActorSystemName, builder => builder.WithActors((system, registry, resolver) =>
        {
            var supervisor = system.ActorOf(resolver.Props<MatchSupervisorActor>(), "matches");
            registry.Register<MatchSupervisorActor>(supervisor);
        }));

        return services;
    }

    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[$"{StorageOptions.SectionName}:{nameof(StorageOptions.Provider)}"] ?? StorageOptions.InMemoryProvider;
        if (string.Equals(provider, StorageOptions.InMemoryProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IMatchStore, InMemoryMatchStore>();
            return;
        }

        if (!string.Equals(provider, StorageOptions.CosmosProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Storage:Provider must be '{StorageOptions.InMemoryProvider}' or '{StorageOptions.CosmosProvider}', not '{provider}'.");
        }

        services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value.Cosmos;
            var clientOptions = new CosmosClientOptions { Serializer = new ContractCosmosSerializer(), ApplicationName = "pitchside" };

            // Locally the Aspire AppHost runs the Cosmos DB emulator and injects its connection string at run time;
            // the emulator has no Entra ID. Everywhere else the account endpoint and a token credential are used.
            var emulator = configuration.GetConnectionString(CosmosEmulatorConnectionName);
            if (!string.IsNullOrWhiteSpace(emulator))
            {
                clientOptions.ConnectionMode = ConnectionMode.Gateway;
                return new CosmosClient(emulator, clientOptions);
            }

            var endpoint = options.AccountEndpoint ?? throw new InvalidOperationException("Storage:Cosmos:AccountEndpoint is not configured.");
            return new CosmosClient(endpoint.ToString(), sp.GetRequiredService<TokenCredential>(), clientOptions);
        });
        services.TryAddSingleton(sp => new CosmosMatchStore(sp.GetRequiredService<CosmosClient>(), sp.GetRequiredService<IOptions<StorageOptions>>().Value.Cosmos.Database));
        services.TryAddSingleton<IMatchStore>(sp => sp.GetRequiredService<CosmosMatchStore>());
        services.AddHostedService<CosmosInitializer>();
    }

    private static void AddBus(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[$"{BusOptions.SectionName}:{nameof(BusOptions.Provider)}"] ?? BusOptions.InProcessProvider;
        if (string.Equals(provider, BusOptions.InProcessProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IEventBus, InProcessEventBus>();
        }
        else if (string.Equals(provider, BusOptions.EventHubsProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IEventBus>(sp => new EventHubsEventBus(
                sp.GetRequiredService<IOptions<BusOptions>>().Value.EventHubs,
                sp.GetRequiredService<TokenCredential>(),
                sp.GetRequiredService<DurableProgress>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EventHubsEventBus>>()));
        }
        else
        {
            throw new InvalidOperationException($"Bus:Provider must be '{BusOptions.InProcessProvider}' or '{BusOptions.EventHubsProvider}', not '{provider}'.");
        }
    }
}
