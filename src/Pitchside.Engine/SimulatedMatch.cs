using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>A complete simulated match: the match.json document and every event in order.</summary>
public sealed record SimulatedMatch(MatchDocument Document, IReadOnlyList<MatchEvent> Events)
{
    public int HomeScore => Events.Count(e => e.Type == EventType.Goal && e.TeamId == Document.Home.Club.Id);

    public int AwayScore => Events.Count(e => e.Type == EventType.Goal && e.TeamId == Document.Away.Club.Id);
}
