namespace Pitchside.Runtime.Messages;

/// <summary>A client connection stops watching the match.</summary>
public sealed record Unsubscribe(string MatchId, string ConnectionId) : IMatchMessage;
