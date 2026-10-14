namespace Pitchside.Runtime.Messages;

/// <summary>Asks for the actor's status, fact sheet and moments in one reply, so a snapshot is consistent.</summary>
public sealed record GetLiveState(string MatchId) : IMatchMessage;
