using System.Collections.Immutable;
using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments;

/// <summary>
/// Everything the rules need to remember between events. Immutable: <see cref="IMomentDetector.Apply"/> returns a
/// new instance.
/// </summary>
public sealed record DetectorState
{
    public required string MatchId { get; init; }
    public required string HomeId { get; init; }
    public required string AwayId { get; init; }
    public required ImmutableDictionary<string, string> ClubNames { get; init; }
    public required ImmutableDictionary<string, string> PlayerNames { get; init; }
    public required ImmutableDictionary<string, string> PlayerTeams { get; init; }

    public int Period { get; init; }

    /// <summary>The last five minutes of the current period.</summary>
    public RollingWindow<MatchEvent> Recent { get; init; } = new(5 * 60_000);

    /// <summary>The first event of the possession in progress.</summary>
    public MatchEvent? PossessionStart { get; init; }

    /// <summary>The most recent foul, for red-card and penalty windows.</summary>
    public MatchEvent? LastFoul { get; init; }

    /// <summary>Hard passes in the possession in progress, waiting to see whether it ends in a shot.</summary>
    public ImmutableList<MatchEvent> PendingHardPasses { get; init; } = [];

    /// <summary>Goal events so far, in order.</summary>
    public ImmutableList<MatchEvent> Goals { get; init; } = [];

    /// <summary>Sequence number of the first event of each period.</summary>
    public ImmutableDictionary<int, int> PeriodFirstSeq { get; init; } = ImmutableDictionary<int, int>.Empty;

    public double PreviousMomentumHome { get; init; } = 0.5;
    public double PreviousMomentumAway { get; init; } = 0.5;

    public bool CleanSheetChecked { get; init; }

    public MomentRateLimiter Limiter { get; init; } = MomentRateLimiter.Empty;

    public string ClubName(string? teamId) => teamId is not null && ClubNames.TryGetValue(teamId, out var name) ? name : string.Empty;

    public string PlayerName(string? playerId) => playerId is not null && PlayerNames.TryGetValue(playerId, out var name) ? name : string.Empty;

    public string Scope(string teamId) => teamId == HomeId ? FactKeys.HomeScope : FactKeys.AwayScope;
}
