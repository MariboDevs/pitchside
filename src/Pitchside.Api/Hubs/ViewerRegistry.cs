using System.Collections.Concurrent;
using Pitchside.Contracts;

namespace Pitchside.Api.Hubs;

/// <summary>Each connection's viewer preferences per match, set with <c>SetViewer</c> and read by WP-06.</summary>
public sealed class ViewerRegistry
{
    private readonly ConcurrentDictionary<(string ConnectionId, string MatchId), ViewerPreferences> _viewers = new();

    public ViewerPreferences Get(string connectionId, string matchId) =>
        _viewers.GetValueOrDefault((connectionId, matchId), ViewerPreferences.Default);

    public void Set(string connectionId, string matchId, ViewerPreferences preferences) => _viewers[(connectionId, matchId)] = preferences;

    public void Remove(string connectionId, string matchId) => _viewers.TryRemove((connectionId, matchId), out _);

    public void RemoveConnection(string connectionId)
    {
        foreach (var key in _viewers.Keys.Where(k => k.ConnectionId == connectionId))
        {
            _viewers.TryRemove(key, out _);
        }
    }
}
