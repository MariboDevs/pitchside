using Pitchside.Contracts;

namespace Pitchside.Evaluation;

/// <summary>
/// What a narrative is scored against: its moment (and the evidence on it), the fact sheet it was written from, and
/// the match document, which names the clubs and players the facts' <c>home</c>, <c>away</c> and player ids refer to.
/// </summary>
public sealed record EvaluationItem(Narrative Narrative, Moment Moment, FactSheet Sheet, MatchDocument? Document = null);
