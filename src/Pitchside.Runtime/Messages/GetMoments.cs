namespace Pitchside.Runtime.Messages;

/// <summary>Asks a match actor for every moment detected so far.</summary>
public sealed record GetMoments(string MatchId) : IMatchMessage;
