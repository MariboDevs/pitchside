using Pitchside.Contracts;

namespace Pitchside.Runtime.Messages;

/// <summary>Internal: events read back from the store to fill a gap in the sequence.</summary>
public sealed record ResendLoaded(IReadOnlyList<MatchEvent> Events);
