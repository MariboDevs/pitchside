using Pitchside.Agents.Providers;

namespace Pitchside.Agents;

/// <summary>The <c>Foundry</c> configuration section. No key is ever configured: the provider is keyless.</summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>The Azure OpenAI or Foundry resource endpoint, such as <c>https://name.openai.azure.com/</c>.</summary>
    public Uri? Endpoint { get; set; }

    public FoundryDeployments Deployments { get; set; } = new();

    public FoundryReasoningEffort ReasoningEffort { get; set; } = new();

    /// <summary>
    /// Deadline for one call on the fast tier, in seconds. A call past it is abandoned and treated as a failed
    /// attempt, so the retry and the template fallback still land inside the latency budget.
    /// </summary>
    public double TimeoutSeconds { get; set; } = 4;

    /// <summary>
    /// Deadline for one call on the quality tier (half-time and full-time narratives, the agent fact-check and the
    /// groundedness judge), in seconds. Quality models reason before they answer, so they need longer than the fast tier.
    /// </summary>
    public double QualityTimeoutSeconds { get; set; } = 12;

    public TimeSpan TimeoutFor(DeploymentTier tier) => TimeSpan.FromSeconds(tier == DeploymentTier.Quality ? QualityTimeoutSeconds : TimeoutSeconds);
}
