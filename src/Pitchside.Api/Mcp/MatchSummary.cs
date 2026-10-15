using Pitchside.Contracts;

namespace Pitchside.Api.Mcp;

/// <summary>One match as <c>list_matches</c> returns it: the fixture, its status, and score and clock from its latest stored sheet.</summary>
public sealed record MatchSummary(
    string MatchId,
    string Home,
    string Away,
    MatchStatus Status,
    int LastSeq,
    int? HomeScore,
    int? AwayScore,
    int? Period,
    int? ClockMs);
