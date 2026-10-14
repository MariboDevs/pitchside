using Pitchside.Stats;

namespace Pitchside.Moments.Rules;

/// <summary>The fact keys a half-time and a full-time summary rest on.</summary>
public static class SummaryKeys
{
    public static IReadOnlyList<string> HalfTime { get; } =
    [
        FactKeys.MatchScoreHome, FactKeys.MatchScoreAway,
        FactKeys.ForSide(FactKeys.HomeScope, FactKeys.Xg), FactKeys.ForSide(FactKeys.AwayScope, FactKeys.Xg),
        FactKeys.ForSide(FactKeys.HomeScope, FactKeys.Shots), FactKeys.ForSide(FactKeys.AwayScope, FactKeys.Shots),
        FactKeys.ForSide(FactKeys.HomeScope, FactKeys.PossessionShare), FactKeys.ForSide(FactKeys.AwayScope, FactKeys.PossessionShare),
        FactKeys.MatchMomentumHome, FactKeys.MatchMomentumAway,
    ];

    public static IReadOnlyList<string> FullTime { get; } =
    [
        FactKeys.MatchScoreHome, FactKeys.MatchScoreAway,
        FactKeys.ForSide(FactKeys.HomeScope, FactKeys.Xg), FactKeys.ForSide(FactKeys.AwayScope, FactKeys.Xg),
        FactKeys.ForSide(FactKeys.HomeScope, FactKeys.Shots), FactKeys.ForSide(FactKeys.AwayScope, FactKeys.Shots),
        FactKeys.ForSide(FactKeys.HomeScope, FactKeys.PossessionShare), FactKeys.ForSide(FactKeys.AwayScope, FactKeys.PossessionShare),
        FactKeys.ForSide(FactKeys.HomeScope, FactKeys.PassCompletion), FactKeys.ForSide(FactKeys.AwayScope, FactKeys.PassCompletion),
    ];
}
