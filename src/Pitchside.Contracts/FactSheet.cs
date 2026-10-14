namespace Pitchside.Contracts;

/// <summary>Every fact true as of event <see cref="AsOfSeq"/>. A sheet at sequence 0 is the pre-match sheet.</summary>
public sealed record FactSheet(
    string MatchId,
    int AsOfSeq,
    int Period,
    int ClockMs,
    IReadOnlyList<Fact> Facts)
{
    /// <summary>Finds a fact by key, or null when the sheet does not hold it.</summary>
    public Fact? Find(string key) => Facts.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.Ordinal));
}
