using Pitchside.Contracts;

namespace Pitchside.Runtime.Messages;

/// <summary>One event for the match actor to ingest. Applied only when its seq is exactly the last seq plus one.</summary>
public sealed record IngestEvent(MatchEvent Event) : IMatchMessage
{
    public string MatchId => Event.MatchId;
}
