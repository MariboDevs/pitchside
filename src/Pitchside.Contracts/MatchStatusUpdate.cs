namespace Pitchside.Contracts;

/// <summary>The <c>MatchStatus</c> SignalR message: sent on state changes and every 10 seconds.</summary>
public sealed record MatchStatusUpdate(string MatchId, MatchStatus Status, int LastSeq, int Period, int ClockMs);
