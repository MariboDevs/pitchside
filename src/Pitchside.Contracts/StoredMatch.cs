namespace Pitchside.Contracts;

/// <summary>
/// The <c>matches</c> document: the match document plus where the match is and how far it has got, and its time scale:
/// milliseconds of match time per real millisecond while it plays (its replay speed; 1 in real time, for an instant
/// replay, and for a document stored before the field existed). The overlay's card timings are scaled by it, so it is
/// stored with the match and read back after a restart.
/// </summary>
public sealed record StoredMatch(MatchDocument Document, MatchStatus Status, int LastSeq, DateTimeOffset CreatedUtc, double TimeScale = 1)
{
    /// <summary>
    /// The largest time scale stored: a replay faster than this plays a match in about a minute, so its cards gain
    /// nothing from a longer scale, and the scaled 45 s expiry (4.5 million ms) keeps the int match clock far from
    /// overflowing.
    /// </summary>
    public const double MaxTimeScale = 100;

    public string MatchId => Document.MatchId;
}
