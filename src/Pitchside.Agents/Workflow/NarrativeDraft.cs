using Pitchside.Contracts;

namespace Pitchside.Agents.Workflow;

/// <summary>One explainer attempt: the sentences it wrote, how, and the error if the call or its output failed.</summary>
public sealed record NarrativeDraft(int Attempt, IReadOnlyList<NarrativeSentence> Sentences, NarrativeProvenance Provenance, string? Error);
