namespace Pitchside.Runtime.Replay;

/// <summary>A replay that has started: which match, how many events it will publish, and at what speed.</summary>
public sealed record ReplayStarted(string MatchId, long Seed, double Speed, int Events);
