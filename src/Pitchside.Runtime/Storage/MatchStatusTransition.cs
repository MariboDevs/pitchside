using Pitchside.Contracts;

namespace Pitchside.Runtime.Storage;

/// <summary>
/// The one rule every store applies to a status write. A finished or stopped match stays that way: the match actor
/// can still be draining events published before a stop, and its routine <c>Live</c> write must not undo the
/// replay's <c>Stopped</c>.
/// </summary>
public static class MatchStatusTransition
{
    public static MatchStatus Resolve(MatchStatus current, MatchStatus requested) =>
        current is MatchStatus.Completed or MatchStatus.Stopped ? current : requested;
}
