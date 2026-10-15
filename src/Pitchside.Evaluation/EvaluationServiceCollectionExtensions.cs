using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pitchside.Runtime.Audit;

namespace Pitchside.Evaluation;

/// <summary>
/// Registers the groundedness provider chosen by <c>Evaluation:Provider</c>. <c>Local</c> needs nothing;
/// <c>Foundry</c> needs the judge, an <see cref="IChatClient"/> registered under the key <see cref="JudgeKey"/>.
/// </summary>
public static class EvaluationServiceCollectionExtensions
{
    public const string JudgeKey = "evaluation-judge";

    public static IServiceCollection AddPitchsideEvaluation(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<EvaluationOptions>(configuration.GetSection(EvaluationOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAuditStore, InMemoryAuditStore>();

        var provider = configuration[$"{EvaluationOptions.SectionName}:{nameof(EvaluationOptions.Provider)}"] ?? EvaluationOptions.LocalProvider;
        if (string.Equals(provider, EvaluationOptions.FoundryProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IEvaluationProvider>(sp => new FoundryEvaluationProvider(
                sp.GetRequiredKeyedService<IChatClient>(JudgeKey), sp.GetRequiredService<IAuditStore>(), sp.GetRequiredService<TimeProvider>()));
        }
        else if (string.Equals(provider, EvaluationOptions.LocalProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IEvaluationProvider, LocalEvaluationProvider>();
        }
        else
        {
            throw new InvalidOperationException($"Evaluation:Provider must be '{EvaluationOptions.LocalProvider}' or '{EvaluationOptions.FoundryProvider}', not '{provider}'.");
        }

        return services;
    }
}
