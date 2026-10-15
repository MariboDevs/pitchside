using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Pitchside.Agents;
using Pitchside.Agents.Providers;
using Pitchside.Evaluation;

namespace Pitchside.Api.Broadcast;

/// <summary>
/// With <c>Evaluation:Provider=Foundry</c>, registers the groundedness judge: a chat client on the Foundry deployment in
/// <c>Evaluation:Deployment</c>, or the crew's quality deployment, under managed identity. Otherwise it adds nothing.
/// </summary>
public static class EvaluationJudge
{
    public static IServiceCollection AddFoundryEvaluationJudge(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var provider = configuration[$"{EvaluationOptions.SectionName}:{nameof(EvaluationOptions.Provider)}"];
        if (!string.Equals(provider, EvaluationOptions.FoundryProvider, StringComparison.OrdinalIgnoreCase))
        {
            return services;
        }

        services.Configure<FoundryOptions>(configuration.GetSection(FoundryOptions.SectionName));
        services.AddKeyedSingleton<IChatClient>(EvaluationServiceCollectionExtensions.JudgeKey, (sp, _) =>
        {
            var credential = sp.GetService<TokenCredential>() ?? new DefaultAzureCredential();
            var foundry = new FoundryChatProvider(sp.GetRequiredService<IOptions<FoundryOptions>>(), credential);
            return foundry.ChatClientFor(sp.GetRequiredService<IOptions<EvaluationOptions>>().Value.Deployment);
        });
        return services;
    }
}
