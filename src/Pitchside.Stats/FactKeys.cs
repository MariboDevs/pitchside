namespace Pitchside.Stats;

/// <summary>
/// The only place a fact key is built. Keys are a scope and a metric: <c>match.*</c>, <c>home.*</c>,
/// <c>away.*</c> and <c>player.&lt;playerId&gt;.*</c>. The fact-checker and the UI import these constants.
/// </summary>
public static class FactKeys
{
    public const string MatchScope = "match";
    public const string HomeScope = "home";
    public const string AwayScope = "away";
    public const string PlayerScope = "player";

    // Match-scoped keys.
    public const string MatchClockMinute = "match.clock.minute";
    public const string MatchPeriod = "match.period";
    public const string MatchState = "match.state";
    public const string MatchScoreHome = "match.score.home";
    public const string MatchScoreAway = "match.score.away";
    public const string MatchPossessions = "match.possessions";
    public const string MatchRhythm = "match.rhythm";
    public const string MatchControl = "match.control";
    public const string MatchMomentumHome = "match.momentum.home";
    public const string MatchMomentumAway = "match.momentum.away";

    // Side metrics, used as home.<metric> and away.<metric>.
    public const string Goals = "goals";
    public const string Xg = "xg";
    public const string Shots = "shots";
    public const string ShotsOnTarget = "shotsOnTarget";
    public const string BigChances = "bigChances";
    public const string Passes = "passes";
    public const string PassesCompleted = "passesCompleted";
    public const string PassCompletion = "passCompletion";
    public const string PassDifficultyAvg = "passDifficultyAvg";
    public const string ProgressivePasses = "progressivePasses";
    public const string FinalThirdEntries = "finalThirdEntries";
    public const string PossessionShare = "possessionShare";
    public const string Corners = "corners";
    public const string Fouls = "fouls";
    public const string Yellows = "yellows";
    public const string Reds = "reds";
    public const string Pressures = "pressures";
    public const string PressingIntensity = "pressingIntensity";
    public const string DistanceKm = "distanceKm";
    public const string TopSpeedKph = "topSpeedKph";

    // Player-only metrics, used as player.<id>.<metric> alongside the shared ones above.
    public const string Carries = "carries";
    public const string Assists = "assists";
    public const string Cards = "cards";
    public const string Minutes = "minutes";
    public const string Sprints = "sprints";
    public const string Tags = "tags";

    // Match states, the values of match.state.
    public const string StatePreMatch = "pre_match";
    public const string StateFirstHalf = "first_half";
    public const string StateHalfTime = "half_time";
    public const string StateSecondHalf = "second_half";
    public const string StateFullTime = "full_time";

    /// <summary>A side-scoped key: <c>home.xg</c>, <c>away.shots</c>.</summary>
    public static string ForSide(string scope, string metric)
    {
        if (scope is not (HomeScope or AwayScope))
        {
            throw new ArgumentException($"A side scope is '{HomeScope}' or '{AwayScope}', not '{scope}'.", nameof(scope));
        }

        return $"{scope}.{metric}";
    }

    /// <summary>A player-scoped key: <c>player.AMZ-09.topSpeedKph</c>.</summary>
    public static string ForPlayer(string playerId, string metric)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        return $"{PlayerScope}.{playerId}.{metric}";
    }

    /// <summary>The momentum key for a side scope.</summary>
    public static string Momentum(string scope) => scope switch
    {
        HomeScope => MatchMomentumHome,
        AwayScope => MatchMomentumAway,
        _ => throw new ArgumentException($"A side scope is '{HomeScope}' or '{AwayScope}', not '{scope}'.", nameof(scope)),
    };

    /// <summary>The score key for a side scope.</summary>
    public static string Score(string scope) => scope switch
    {
        HomeScope => MatchScoreHome,
        AwayScope => MatchScoreAway,
        _ => throw new ArgumentException($"A side scope is '{HomeScope}' or '{AwayScope}', not '{scope}'.", nameof(scope)),
    };
}
