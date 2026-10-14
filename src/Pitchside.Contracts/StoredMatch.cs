namespace Pitchside.Contracts;

/// <summary>The <c>matches</c> document: the match document plus where the match is and how far it has got.</summary>
public sealed record StoredMatch(MatchDocument Document, MatchStatus Status, int LastSeq, DateTimeOffset CreatedUtc)
{
    public string MatchId => Document.MatchId;
}
