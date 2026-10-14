using System.Collections.Immutable;
using Pitchside.Contracts;

namespace Pitchside.Stats;

/// <summary>Immutable accumulators for one match as of one event. <see cref="IMatchStatsEngine.Apply"/> returns a new instance.</summary>
public sealed record MatchStatsState
{
    public required string MatchId { get; init; }
    public required string HomeId { get; init; }
    public required string AwayId { get; init; }

    /// <summary>Every squad member's team, from the match document.</summary>
    public required ImmutableDictionary<string, string> Roster { get; init; }

    public required ImmutableArray<string> HomeStarters { get; init; }
    public required ImmutableArray<string> AwayStarters { get; init; }

    public int AsOfSeq { get; init; }
    public int Period { get; init; }
    public int ClockMs { get; init; }
    public string State { get; init; } = FactKeys.StatePreMatch;
    public int LastPossessionId { get; init; }
    public int Possessions { get; init; }

    /// <summary>The team the latest possession-revealing event put in possession; null before kick-off.</summary>
    public string? InPossession { get; init; }

    public SideAccumulator Home { get; init; } = new();
    public SideAccumulator Away { get; init; } = new();

    /// <summary>Players who have appeared, ordered by id.</summary>
    public ImmutableSortedDictionary<string, PlayerAccumulator> Players { get; init; } =
        ImmutableSortedDictionary.Create<string, PlayerAccumulator>(StringComparer.Ordinal);

    /// <summary>The last ten minutes of events in the current period.</summary>
    public RollingWindow<MatchEvent> Window { get; init; } = new(EventRules.TenMinutesMs);

    public SideAccumulator Side(string teamId) => teamId == HomeId ? Home : Away;

    public string Scope(string teamId) => teamId == HomeId ? FactKeys.HomeScope : FactKeys.AwayScope;
}
