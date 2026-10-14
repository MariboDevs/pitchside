namespace Pitchside.Contracts;

/// <summary>Who wrote a narrative and how: provider, model deployment, prompt version, attempts, fallback.</summary>
public sealed record NarrativeProvenance(
    string Provider,
    string Model,
    string PromptVersion,
    int Attempts,
    DateTimeOffset GeneratedUtc,
    bool Fallback);
