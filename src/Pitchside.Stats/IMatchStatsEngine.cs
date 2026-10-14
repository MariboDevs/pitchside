using Pitchside.Contracts;

namespace Pitchside.Stats;

/// <summary>The incremental stats engine. Pure: the same events always produce the same sheets.</summary>
public interface IMatchStatsEngine
{
    MatchStatsState Start(MatchDocument document);

    StepResult Apply(MatchStatsState state, MatchEvent matchEvent);

    FactSheet Snapshot(MatchStatsState state);
}
