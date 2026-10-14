namespace Pitchside.Agents;

/// <summary>Model deployment names: Fast for explainer, persona and translator; Quality for summaries and checking.</summary>
public sealed class FoundryDeployments
{
    public string? Fast { get; set; }

    public string? Quality { get; set; }
}
