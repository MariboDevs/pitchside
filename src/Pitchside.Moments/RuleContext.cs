using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments;

/// <summary>
/// What a rule sees for one event: the state before it, the state after the event was recorded, the event, the
/// fact sheet as of the event, and the tags it raised.
/// </summary>
public sealed record RuleContext(
    DetectorState Previous,
    DetectorState Current,
    MatchEvent Event,
    FactSheet Sheet,
    IReadOnlyList<Tag> Tags);
