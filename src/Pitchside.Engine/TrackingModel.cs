using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>
/// Per-player movement between tracking ticks. Distance is the player's work rate over the minutes on the pitch
/// plus the length of their carries; top speed is the faster of their carries and a sampled off-ball sprint.
/// It draws from its own generator so that tracking never perturbs the match itself.
/// </summary>
internal sealed class TrackingModel
{
    private readonly Pcg32 _rng;
    private readonly Dictionary<string, int> _sinceMs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _carryDistanceM = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _carryTopSpeedKph = new(StringComparer.Ordinal);

    public TrackingModel(long seed)
    {
        _rng = new Pcg32(unchecked((ulong)seed), 77UL);
    }

    /// <summary>Starts a fresh interval for every listed player at the given period clock.</summary>
    public void StartInterval(IEnumerable<Player> players, int clockMs)
    {
        ArgumentNullException.ThrowIfNull(players);
        foreach (var player in players)
        {
            PlayerOn(player.Id, clockMs);
        }
    }

    public void PlayerOn(string playerId, int clockMs)
    {
        _sinceMs[playerId] = clockMs;
        _carryDistanceM.Remove(playerId);
        _carryTopSpeedKph.Remove(playerId);
    }

    public void Carry(string playerId, double lengthM, double speedKph)
    {
        _carryDistanceM[playerId] = _carryDistanceM.GetValueOrDefault(playerId) + lengthM;
        _carryTopSpeedKph[playerId] = Math.Max(_carryTopSpeedKph.GetValueOrDefault(playerId), speedKph);
    }

    /// <summary>Closes the interval for the players on the pitch and returns each one's delta.</summary>
    public IReadOnlyList<TrackingEntry> Tick(IReadOnlyList<Player> onPitch, int clockMs)
    {
        ArgumentNullException.ThrowIfNull(onPitch);
        var entries = new List<TrackingEntry>(onPitch.Count);
        foreach (var player in onPitch)
        {
            var since = _sinceMs.GetValueOrDefault(player.Id, clockMs);
            var minutes = Math.Max(0, clockMs - since) / 60_000.0;
            var distance = (player.WorkRate * minutes) + _carryDistanceM.GetValueOrDefault(player.Id);
            var mean = player.Position is Position.Forward or Position.Defender ? 23.0 : 21.0;
            var offBall = Math.Clamp(_rng.Gaussian(mean, 3), 12, 34);
            var top = Math.Max(offBall, _carryTopSpeedKph.GetValueOrDefault(player.Id));
            entries.Add(new TrackingEntry(player.Id, Math.Round(Math.Max(distance, 0.1), 1), Math.Round(top, 1)));
            PlayerOn(player.Id, clockMs);
        }

        return entries;
    }
}
