namespace Pitchside.Contracts;

/// <summary>
/// How well a narrative is grounded. <see cref="CitationCoverage"/> is the share of sentences that cite at least
/// one fact; <see cref="Groundedness"/> is filled by the evaluation pass (WP-07) and null until then.
/// </summary>
public sealed record NarrativeEvaluation(double CitationCoverage, double? Groundedness);
