namespace Pitchside.Agents;

/// <summary>The <c>Agents</c> configuration section.</summary>
public sealed class AgentsOptions
{
    public const string SectionName = "Agents";
    public const string ScriptedProvider = "Scripted";
    public const string FoundryProvider = "Foundry";

    /// <summary><c>Scripted</c> (offline, the default) or <c>Foundry</c>.</summary>
    public string Provider { get; set; } = ScriptedProvider;

    /// <summary>At most this many moment workflows run at once per host; the rest wait in the sink's queue.</summary>
    public int MaxConcurrentMoments { get; set; } = 2;

    /// <summary>Whether the second, model-based fact-check pass runs.</summary>
    public bool AgentFactCheck { get; set; } = true;
}
