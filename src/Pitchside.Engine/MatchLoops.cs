using System.Globalization;
using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>
/// Later replays of a seed under their own match id (the WP-11 demo loop). A match id is replayed once per store, so
/// loop 1 is the seed's own id, <c>MER-26</c>, and loop 2 onwards is <c>MER-26-2</c>, <c>MER-26-3</c>. The match itself is
/// the seed's: only its ids change.
/// </summary>
public static class MatchLoops
{
    public const int First = 1;

    /// <summary>The id of a seed's replay in the given loop.</summary>
    public static string MatchIdFor(long seed, int loop)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(loop, First);
        return loop == First ? MatchSimulator.MatchIdFor(seed) : string.Create(CultureInfo.InvariantCulture, $"MER-{seed}-{loop}");
    }

    /// <summary>
    /// The same match under another id: the document's <c>matchId</c>, and every event's <c>matchId</c> and <c>id</c>
    /// (<c>{matchId}-{seq}</c>). Nothing else changes, so the stats, moments and narratives are the seed's.
    /// </summary>
    public static SimulatedMatch As(SimulatedMatch match, string matchId)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        var from = match.Document.MatchId;
        if (from == matchId)
        {
            return match;
        }

        var prefix = from + "-";
        return new SimulatedMatch(
            match.Document with { MatchId = matchId },
            [.. match.Events.Select(e => e with
            {
                MatchId = matchId,
                Id = e.Id.StartsWith(prefix, StringComparison.Ordinal)
                    ? string.Concat(matchId, "-", e.Id.AsSpan(prefix.Length))
                    : throw new InvalidOperationException($"Event {e.Id} does not belong to {from}."),
            })]);
    }
}
