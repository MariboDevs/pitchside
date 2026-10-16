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
using Pitchside.Agents.Speech;
using Pitchside.Agents.Workflow;
using Pitchside.Runtime.Audit;
using Pitchside.Runtime.MomentSink;
using Pitchside.Runtime.Recaps;
using Pitchside.Runtime.Usage;

namespace Pitchside.Agents;

/// <summary>Registers the agent crew. The provider is chosen by <c>Agents:Provider</c>; <c>Scripted</c> needs no network.</summary>
public static class AgentsServiceCollectionExtensions
{
    public static IServiceCollection AddPitchsideAgents(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var agents = configuration.GetSection(AgentsOptions.SectionName);
        services.Configure<AgentsOptions>(options =>
        {
            agents.Bind(options);

            // The binder appends configured array entries to the defaults; a configured list replaces them instead.
            options.Personas = Replace(agents.GetSection(nameof(AgentsOptions.Personas)), options.Personas);
            options.Languages = Replace(agents.GetSection(nameof(AgentsOptions.Languages)), options.Languages);
        });
        services.Configure<FoundryOptions>(configuration.GetSection(FoundryOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PromptCatalog>();
        services.AddUsageGuard(configuration);
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
        AddSpeech(services, configuration);
        services.TryAddSingleton<WorkflowGate>();

        var provider = configuration[$"{AgentsOptions.SectionName}:{nameof(AgentsOptions.Provider)}"] ?? AgentsOptions.ScriptedProvider;
        // Every model call goes through the auditing wrapper (WP07-R6). The audit store is the runtime's when it is
        // registered (the API, the agent host); otherwise, as in the CLI, an in-process one.
        services.TryAddSingleton<IAuditStore, InMemoryAuditStore>();
        if (string.Equals(provider, AgentsOptions.FoundryProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
            services.TryAddSingleton<IChatProvider>(sp => Audited(sp, new FoundryChatProvider(sp.GetRequiredService<IOptions<FoundryOptions>>(), sp.GetRequiredService<TokenCredential>())));
        }
        else if (string.Equals(provider, AgentsOptions.ScriptedProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IChatProvider>(sp => Audited(sp, new ScriptedChatProvider()));
        }
        else
        {
            throw new InvalidOperationException($"Agents:Provider must be '{AgentsOptions.ScriptedProvider}' or '{AgentsOptions.FoundryProvider}', not '{provider}'.");
        }

        return services;
    }

    /// <summary>
    /// The spoken recap (WP-10), chosen by <c>Speech:Provider</c>: <c>None</c> (the default) registers no speech provider,
    /// so the recap stage does nothing; <c>Scripted</c> writes a silent clip; <c>Azure</c> calls Azure AI Speech keyless.
    /// The recap store is the runtime's when it is registered (the API, the agent host), otherwise in process.
    /// </summary>
    private static void AddSpeech(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SpeechOptions>(configuration.GetSection(SpeechOptions.SectionName));
        services.TryAddSingleton<IRecapStore, InMemoryRecapStore>();
        services.TryAddSingleton<RecapWriter>();
        var provider = configuration[$"{SpeechOptions.SectionName}:{nameof(SpeechOptions.Provider)}"] ?? SpeechOptions.NoneProvider;
        if (string.Equals(provider, SpeechOptions.ScriptedProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<ISpeechProvider, ScriptedSpeechProvider>();
        }
        else if (string.Equals(provider, SpeechOptions.AzureProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
            services.TryAddSingleton<ISpeechProvider>(sp => new AzureSpeechProvider(
                new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                sp.GetRequiredService<TokenCredential>(),
                sp.GetRequiredService<IOptions<SpeechOptions>>()));
        }
        else if (!string.Equals(provider, SpeechOptions.NoneProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Speech:Provider must be '{SpeechOptions.NoneProvider}', '{SpeechOptions.ScriptedProvider}' or '{SpeechOptions.AzureProvider}', not '{provider}'.");
        }
    }

    private static AuditingChatProvider Audited(IServiceProvider sp, IChatProvider inner) => new(
        inner,
        sp.GetRequiredService<IAuditStore>(),
        sp.GetRequiredService<TimeProvider>(),
        sp.GetService<Microsoft.Extensions.Logging.ILogger<AuditingChatProvider>>());

    /// <summary>Adds the hosted service that reads the moment sink and runs the workflows.</summary>
    public static IServiceCollection AddMomentWorkflowRunner(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<MomentWorkflowRunner>();
        services.AddHostedService(sp => sp.GetRequiredService<MomentWorkflowRunner>());
        return services;
    }

    /// <summary>The configured list when the setting is present (an empty value gives an empty list), else the defaults.</summary>
    private static string[] Replace(IConfigurationSection section, string[] defaults) =>
        section.Exists() ? section.GetChildren().Select(c => c.Value).OfType<string>().ToArray() : defaults;
}
