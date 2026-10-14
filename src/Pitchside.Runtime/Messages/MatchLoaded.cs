using Pitchside.Contracts;

namespace Pitchside.Runtime.Messages;

/// <summary>Internal: the match and its stored events, read at start so the actor can rebuild its state.</summary>
public sealed record MatchLoaded(StoredMatch? Match, IReadOnlyList<MatchEvent> Events);
