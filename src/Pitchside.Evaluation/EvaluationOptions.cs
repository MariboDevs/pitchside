namespace Pitchside.Evaluation;

/// <summary><c>Evaluation:*</c> settings: the provider, how often a match's narratives are scored, and the judge model.</summary>
public sealed class EvaluationOptions
{
    public const string SectionName = "Evaluation";
    public const string LocalProvider = "Local";
    public const string FoundryProvider = "Foundry";

    public string Provider { get; set; } = LocalProvider;

    /// <summary>Narratives are scored in batches this many seconds apart, to keep the judge's cost down.</summary>
    public int BatchSeconds { get; set; } = 30;

    /// <summary>The Foundry deployment that judges groundedness; empty means the crew's quality deployment.</summary>
    public string Deployment { get; set; } = string.Empty;
}
