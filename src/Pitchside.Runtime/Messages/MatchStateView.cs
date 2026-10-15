using Pitchside.Contracts;

namespace Pitchside.Runtime.Messages;

/// <summary>A match actor's reply to <see cref="GetState"/>: a snapshot, never the actor's own state.</summary>
public sealed record MatchStateView(string MatchId, MatchStatus Status, int LastSeq, int Period, int ClockMs, int Subscribers, int DroppedEvents, int PendingEvents = 0)
{
    public MatchStatusUpdate ToStatus() => new(MatchId, Status, LastSeq, Period, ClockMs);
}
