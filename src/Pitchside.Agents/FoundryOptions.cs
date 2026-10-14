namespace Pitchside.Agents;

/// <summary>The <c>Foundry</c> configuration section. No key is ever configured: the provider is keyless.</summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>The Azure OpenAI or Foundry resource endpoint, such as <c>https://name.openai.azure.com/</c>.</summary>
    public Uri? Endpoint { get; set; }

    public FoundryDeployments Deployments { get; set; } = new();
}
