namespace Pitchside.Runtime.Replay;

/// <summary>
/// A jump to the next moment (WP-11): the moment ahead, and the event the replay now runs to without waiting, ten
/// seconds of match time before it, from where it plays on at its own speed.
/// </summary>
public sealed record ReplayJump(string MatchId, string MomentId, string MomentType, string Headline, int ToSeq);
