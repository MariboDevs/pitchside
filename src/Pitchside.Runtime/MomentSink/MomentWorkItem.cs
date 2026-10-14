using Pitchside.Contracts;

namespace Pitchside.Runtime.MomentSink;

/// <summary>A detected moment handed to the agent crew, with the fact sheet it was detected at and the match document.</summary>
public sealed record MomentWorkItem(Moment Moment, FactSheet FactSheet, MatchDocument Document);
