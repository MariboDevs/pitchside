using Pitchside.Agents.Providers;

namespace Pitchside.Agents;

/// <summary>
/// Reasoning effort sent with each call, per deployment tier. The GPT-6 generation reasons by default and then
/// rejects a fixed temperature; the fast tier runs at <c>none</c>, which answers inside the latency budget and accepts
/// temperature 0, and the quality tier at <c>low</c>, the smallest value its models accept. An empty value sends
/// nothing and leaves the model at its default.
/// </summary>
public sealed class FoundryReasoningEffort
{
    public const string None = "none";

    public string Fast { get; set; } = None;

    public string Quality { get; set; } = "low";

    public string For(DeploymentTier tier) => tier == DeploymentTier.Quality ? Quality : Fast;
}
