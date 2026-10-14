namespace Pitchside.Runtime.Messages;

/// <summary>A client connection starts watching the match.</summary>
public sealed record Subscribe(string MatchId, string ConnectionId) : IMatchMessage;
