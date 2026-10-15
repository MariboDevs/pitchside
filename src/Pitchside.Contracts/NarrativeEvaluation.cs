namespace Pitchside.Contracts;

/// <summary>
/// How well a narrative is grounded (WP07-R1, R2). <see cref="CitationCoverage"/> is the share of written
/// <c>[[key]]</c> tokens that resolve on the fact sheet, 1 for every verified narrative; <see cref="FactDensity"/>
/// is tokens per sentence; <see cref="Groundedness"/> is filled by the evaluation pass and null until then.
/// </summary>
public sealed record NarrativeEvaluation(double CitationCoverage, double FactDensity, double? Groundedness);
