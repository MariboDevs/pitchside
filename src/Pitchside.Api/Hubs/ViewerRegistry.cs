using System.Collections.Concurrent;
using Pitchside.Contracts;

namespace Pitchside.Api.Hubs;

/// <summary>
/// Per connection: the matches it has joined, and its viewer preferences per match (set with <c>SetViewer</c>, read
/// by WP-06). Joins are tracked apart from preferences so a disconnect can unsubscribe from every joined match.
/// </summary>
public sealed class ViewerRegistry
{
    private readonly ConcurrentDictionary<(string ConnectionId, string MatchId), ViewerPreferences> _viewers = new();
    private readonly ConcurrentDictionary<(string ConnectionId, string MatchId), bool> _joined = new();

    public ViewerPreferences Get(string connectionId, string matchId) =>
        _viewers.GetValueOrDefault((connectionId, matchId), ViewerPreferences.Default);

    public void Set(string connectionId, string matchId, ViewerPreferences preferences) => _viewers[(connectionId, matchId)] = preferences;

    public void Join(string connectionId, string matchId) => _joined[(connectionId, matchId)] = true;

    /// <summary>Forgets the join and the preferences; false when the connection had not joined the match.</summary>
    public bool Leave(string connectionId, string matchId)
    {
        _viewers.TryRemove((connectionId, matchId), out _);
        return _joined.TryRemove((connectionId, matchId), out _);
    }

    /// <summary>Forgets the connection and returns the matches it had joined.</summary>
    public IReadOnlyList<string> RemoveConnection(string connectionId)
    {
        foreach (var key in _viewers.Keys.Where(k => k.ConnectionId == connectionId))
        {
            _viewers.TryRemove(key, out _);
        }

        var joined = new List<string>();
        foreach (var key in _joined.Keys.Where(k => k.ConnectionId == connectionId))
        {
            if (_joined.TryRemove(key, out _))
            {
                joined.Add(key.MatchId);
            }
        }

        return joined;
    }
}
