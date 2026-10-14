using Pitchside.Contracts;

namespace Pitchside.Runtime.Messages;

/// <summary>The producer paused, resumed or stopped the match; the actor records, persists and broadcasts it.</summary>
public sealed record SetLifecycle(string MatchId, MatchStatus Status) : IMatchMessage;
