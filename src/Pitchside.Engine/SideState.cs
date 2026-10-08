using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>Mutable per-team state for one simulation: who is on the pitch, the running tallies, pending substitutions.</summary>
internal sealed class SideState
{
    private readonly List<Player> _onPitch;
    private readonly Queue<Player> _bench;
    private readonly Queue<int> _substitutionMinutes;
    private readonly HashSet<string> _replaced = [];

    public SideState(EngineClub club, TeamSheet sheet, bool isHome, Pcg32 rng)
    {
        Club = club;
        IsHome = isHome;
        _onPitch = [.. sheet.Starters];
        _bench = new Queue<Player>(sheet.Bench.Where(p => p.Position != Position.Goalkeeper));
        var minutes = new[] { rng.Next(29) + 58, rng.Next(29) + 58, rng.Next(29) + 58 };
        Array.Sort(minutes);
        _substitutionMinutes = new Queue<int>(minutes);
    }

    public EngineClub Club { get; }

    public bool IsHome { get; }

    public IReadOnlyList<Player> OnPitch => _onPitch;

    public HashSet<string> Booked { get; } = [];

    public int Goals { get; set; }

    public int Shots { get; set; }

    public int ShotsOnTarget { get; set; }

    public int Corners { get; set; }

    public int Fouls { get; set; }

    public int Yellows { get; set; }

    public int Reds { get; set; }

    public int PlayersSentOff { get; private set; }

    public void SendOff(string playerId)
    {
        _onPitch.RemoveAll(p => p.Id == playerId);
        PlayersSentOff++;
    }

    public bool NextSubstitutionMinute(out int minute) => _substitutionMinutes.TryPeek(out minute);

    public void SkipSubstitution() => _substitutionMinutes.Dequeue();

    public string? PickPlayerToReplace(Pcg32 rng)
    {
        var candidates = _onPitch.Where(p => p.Position != Position.Goalkeeper && !_replaced.Contains(p.Id)).ToList();
        return candidates.Count == 0 ? null : rng.Pick(candidates).Id;
    }

    public string? TakeFromBench(string? playerOff)
    {
        if (playerOff is null || _bench.Count == 0)
        {
            return null;
        }

        return _bench.Peek().Id;
    }

    public void CompleteSubstitution(string playerOff, string playerOn)
    {
        var off = _onPitch.First(p => p.Id == playerOff);
        var on = _bench.Dequeue();
        if (on.Id != playerOn)
        {
            throw new InvalidOperationException("Bench order and substitution request disagree.");
        }

        _onPitch.Remove(off);
        _onPitch.Add(on);
        _replaced.Add(on.Id);
        _substitutionMinutes.Dequeue();
    }
}
