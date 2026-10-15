using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Personalisation;
using Pitchside.Agents.Prompts;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;
using Pitchside.Runtime.MomentSink;

namespace Pitchside.Agents;

/// <summary>Registers the agent crew. The provider is chosen by <c>Agents:Provider</c>; <c>Scripted</c> needs no network.</summary>
public static class AgentsServiceCollectionExtensions
{
    public static IServiceCollection AddPitchsideAgents(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AgentsOptions>(configuration.GetSection(AgentsOptions.SectionName));
        services.Configure<FoundryOptions>(configuration.GetSection(FoundryOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PromptCatalog>();
        services.TryAddSingleton(sp => new CodeFactChecker(sp.GetRequiredService<PromptCatalog>().ForbiddenHashes));
        services.TryAddSingleton(sp => new NarrativeVerifier(
            sp.GetRequiredService<CodeFactChecker>(),
            sp.GetRequiredService<IChatProvider>(),
            sp.GetRequiredService<PromptCatalog>(),
            sp.GetRequiredService<IOptions<AgentsOptions>>().Value.AgentFactCheck));
        services.TryAddSingleton<IPersonaWriter, AgentPersonaWriter>();
        services.TryAddSingleton<ITranslator, AgentTranslator>();
        services.TryAddSingleton<INarrativePublisher, InMemoryNarrativeLog>();
        services.TryAddSingleton<IMomentSink, ChannelMomentSink>();
        services.TryAddSingleton<MomentWorkflow>();

        var provider = configuration[$"{AgentsOptions.SectionName}:{nameof(AgentsOptions.Provider)}"] ?? AgentsOptions.ScriptedProvider;
        if (string.Equals(provider, AgentsOptions.FoundryProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
            services.TryAddSingleton<IChatProvider>(sp => new FoundryChatProvider(sp.GetRequiredService<IOptions<FoundryOptions>>(), sp.GetRequiredService<TokenCredential>()));
        }
        else if (string.Equals(provider, AgentsOptions.ScriptedProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IChatProvider, ScriptedChatProvider>();
        }
        else
        {
            throw new InvalidOperationException($"Agents:Provider must be '{AgentsOptions.ScriptedProvider}' or '{AgentsOptions.FoundryProvider}', not '{provider}'.");
        }

        return services;
    }

    /// <summary>Adds the hosted service that reads the moment sink and runs the workflows.</summary>
    public static IServiceCollection AddMomentWorkflowRunner(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<MomentWorkflowRunner>();
        services.AddHostedService(sp => sp.GetRequiredService<MomentWorkflowRunner>());
        return services;
    }
}
