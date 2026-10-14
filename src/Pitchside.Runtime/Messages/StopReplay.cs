namespace Pitchside.Runtime.Messages;

/// <summary>Stops a running replay.</summary>
public sealed record StopReplay(string MatchId) : IMatchMessage;
