using Pitchside.Contracts;

namespace Pitchside.Runtime.Messages;

/// <summary>
/// Internal: the match, its stored events and the ids of moments that already have a narrative, read at start so the
/// actor can rebuild its state and finish any work a crash interrupted.
/// </summary>
public sealed record MatchLoaded(StoredMatch? Match, IReadOnlyList<MatchEvent> Events, IReadOnlySet<string> NarratedMomentIds);
