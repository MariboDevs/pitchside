namespace Pitchside.Runtime.Messages;

/// <summary>Asks a match actor for its latest fact sheet.</summary>
public sealed record GetFactSheet(string MatchId) : IMatchMessage;
