using Pitchside.Contracts;

namespace Pitchside.Evaluation;

/// <summary>What a narrative is scored against: its moment (and the evidence on it) and the fact sheet it was written from.</summary>
public sealed record EvaluationItem(Narrative Narrative, Moment Moment, FactSheet Sheet);
