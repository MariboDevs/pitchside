using Pitchside.Contracts;

namespace Pitchside.Runtime.Messages;

/// <summary>The reply to <see cref="GetLiveState"/>: one consistent view of the match as the actor holds it.</summary>
public sealed record LiveState(MatchStateView View, FactSheet FactSheet, IReadOnlyList<Moment> Moments);
