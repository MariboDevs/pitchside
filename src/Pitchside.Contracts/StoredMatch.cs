namespace Pitchside.Contracts;

/// <summary>
/// The <c>matches</c> document: the match document plus where the match is and how far it has got, and its time scale:
/// milliseconds of match time per real millisecond while it plays (its replay speed; 1 in real time, for an instant
/// replay, and for a document stored before the field existed). The overlay's card timings are scaled by it, so it is
/// stored with the match and read back after a restart.
/// </summary>
public sealed record StoredMatch(MatchDocument Document, MatchStatus Status, int LastSeq, DateTimeOffset CreatedUtc, double TimeScale = 1)
{
    public string MatchId => Document.MatchId;
}
