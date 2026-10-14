using Pitchside.Contracts;

namespace Pitchside.Agents.Providers;

/// <summary>One model call: the prompt by name and version, the system and user text, and the deployment tier.</summary>
public sealed record ChatRequest(
    string PromptName,
    string PromptVersion,
    string SystemPrompt,
    string UserMessage,
    DeploymentTier Tier,
    Moment Moment,
    int Attempt)
{
    /// <summary>Output cap per call, from the WP-04 token budget.</summary>
    public int MaxOutputTokens { get; init; } = 400;
}
