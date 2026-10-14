namespace Pitchside.Stats;

/// <summary>The state after one event and the tags that event raised, in the order they were raised.</summary>
public sealed record StepResult(MatchStatsState State, IReadOnlyList<Tag> Tags);
