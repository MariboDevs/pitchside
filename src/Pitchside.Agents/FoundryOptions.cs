namespace Pitchside.Agents;

/// <summary>The <c>Foundry</c> configuration section. No key is ever configured: the provider is keyless.</summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>The Azure OpenAI or Foundry resource endpoint, such as <c>https://name.openai.azure.com/</c>.</summary>
    public Uri? Endpoint { get; set; }

    public FoundryDeployments Deployments { get; set; } = new();

    /// <summary>
    /// Deadline for one model call, in seconds. A call past it is abandoned and treated as a failed attempt, so the
    /// retry and the template fallback still land inside the latency budget.
    /// </summary>
    public double TimeoutSeconds { get; set; } = 4;
}
