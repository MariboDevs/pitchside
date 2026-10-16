using Microsoft.Extensions.Options;

namespace Pitchside.Runtime.Usage;

/// <summary>
/// Fixed-window limits per hour (replays per caller, narratives and spoken lines per host) and a concurrent count of
/// hub connections per caller (WP07-R5). Uses are always counted, so the control room can show them; they are only
/// refused when the guard is enabled. Windows start on the hour, by the injected clock.
/// </summary>
public sealed class UsageGuard(IOptions<UsageGuardOptions> options, TimeProvider time, bool enabled)
{
    /// <summary>The key of a per-host limit.</summary>
    public const string HostKey = "host";

    private readonly UsageGuardOptions _options = options.Value;
    private readonly Dictionary<(string Limit, string Key), (long Window, int Count)> _windows = [];
    private readonly Dictionary<string, int> _connections = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private long _currentWindow = -1;

    public bool Enabled => enabled;

    /// <summary>How many hourly counts are held, for tests that check expired ones are dropped.</summary>
    internal int TrackedCounts
    {
        get
        {
            lock (_lock)
            {
                return _windows.Count;
            }
        }
    }

    /// <summary>Counts one use of an hourly limit, refusing it when the limit is reached and the guard is enabled.</summary>
    public UsageDecision TryTake(string limit, string key) => TryTake(limit, key, 1);

    /// <summary>
    /// Counts <paramref name="count"/> uses of an hourly limit at once, all or none: refused when they would not all fit
    /// and the guard is enabled, so two callers can never each take part of what only one of them needs.
    /// </summary>
    public UsageDecision TryTake(string limit, string key, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        lock (_lock)
        {
            // The clock is read under the lock, so two callers at the turn of the hour can never apply their counts out
            // of order; a window never moves backwards.
            var now = time.GetUtcNow().ToUnixTimeSeconds();
            var window = Math.Max(now / 3600, _currentWindow);

            // Once per hour, when a new window starts, every count from an earlier one is dropped, so the table holds
            // only the callers of the current hour rather than every address the host has ever seen.
            if (window != _currentWindow)
            {
                foreach (var stale in _windows.Where(w => w.Value.Window < window).Select(w => w.Key).ToList())
                {
                    _windows.Remove(stale);
                }

                _currentWindow = window;
            }

            var used = _windows.TryGetValue((limit, key), out var current) && current.Window == window ? current.Count : 0;
            if (enabled && used + count > LimitOf(limit))
            {
                return new UsageDecision(false, TimeSpan.FromSeconds(((window + 1) * 3600) - now), window);
            }

            _windows[(limit, key)] = (window, used + count);
            return new UsageDecision(true, TimeSpan.Zero, window);
        }
    }

    /// <summary>
    /// Gives back one use, for a request that was counted but did not start what it counts (a replay refused as
    /// invalid, conflicting or over the concurrent cap). It comes off the hour the use was taken in, the decision's
    /// <see cref="UsageDecision.Window"/>; a use from an hour already gone is not returned to the next one.
    /// </summary>
    public void Release(string limit, string key, long window)
    {
        lock (_lock)
        {
            if (_windows.TryGetValue((limit, key), out var current) && current.Window == window && current.Count > 0)
            {
                _windows[(limit, key)] = (current.Window, current.Count - 1);
            }
        }
    }

    /// <summary>Whether a caller may open another hub connection.</summary>
    public bool CanConnect(string key)
    {
        lock (_lock)
        {
            return !enabled || _connections.GetValueOrDefault(key) < _options.ConnectionsPerIp;
        }
    }

    /// <summary>Checks the limit and reserves a slot in one step; a refused caller holds no slot to release.</summary>
    public bool TryConnect(string key)
    {
        lock (_lock)
        {
            var open = _connections.GetValueOrDefault(key);
            if (enabled && open >= _options.ConnectionsPerIp)
            {
                return false;
            }

            _connections[key] = open + 1;
            return true;
        }
    }

    public void Disconnected(string key)
    {
        lock (_lock)
        {
            var left = _connections.GetValueOrDefault(key) - 1;
            if (left > 0)
            {
                _connections[key] = left;
            }
            else
            {
                _connections.Remove(key);
            }
        }
    }

    /// <summary>Every limit with its current use: the caller's for per-caller limits, the host's otherwise.</summary>
    public IReadOnlyList<UsageCount> Snapshot(string callerKey)
    {
        var window = time.GetUtcNow().ToUnixTimeSeconds() / 3600;
        lock (_lock)
        {
            int Used(string limit, string key) => _windows.TryGetValue((limit, key), out var c) && c.Window == window ? c.Count : 0;
            return
            [
                new(UsageLimits.Replays, "caller per hour", Used(UsageLimits.Replays, callerKey), _options.ReplaysPerHourPerIp),
                new(UsageLimits.Narratives, "host per hour", Used(UsageLimits.Narratives, HostKey), _options.NarrativesPerHour),
                new(UsageLimits.SpokenLines, "host per hour", Used(UsageLimits.SpokenLines, HostKey), _options.SpokenLinesPerHour),
                new(UsageLimits.Connections, "caller concurrent", _connections.GetValueOrDefault(callerKey), _options.ConnectionsPerIp),
            ];
        }
    }

    private int LimitOf(string limit) => limit switch
    {
        UsageLimits.Replays => _options.ReplaysPerHourPerIp,
        UsageLimits.Narratives => _options.NarrativesPerHour,
        UsageLimits.SpokenLines => _options.SpokenLinesPerHour,
        _ => throw new ArgumentOutOfRangeException(nameof(limit), limit, "Not an hourly limit."),
    };
}
