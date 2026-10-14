namespace Pitchside.Runtime.Messages;

/// <summary>Asks a match actor for its <see cref="MatchStateView"/>.</summary>
public sealed record GetState(string MatchId) : IMatchMessage;
