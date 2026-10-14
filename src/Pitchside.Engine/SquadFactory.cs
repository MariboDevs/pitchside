using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>Builds a deterministic team sheet for a club: eleven starters in a 4-3-3 and seven on the bench.</summary>
public static class SquadFactory
{
    private static readonly Position[] StartingPositions =
    [
        Position.Goalkeeper,
        Position.Defender, Position.Defender, Position.Defender, Position.Defender,
        Position.Midfielder, Position.Midfielder, Position.Midfielder,
        Position.Forward, Position.Forward, Position.Forward,
    ];

    private static readonly Position[] BenchPositions =
    [
        Position.Goalkeeper, Position.Defender, Position.Defender, Position.Midfielder, Position.Midfielder,
        Position.Forward, Position.Forward,
    ];

    public static TeamSheet Create(EngineClub club, Pcg32 rng)
    {
        ArgumentNullException.ThrowIfNull(club);
        ArgumentNullException.ThrowIfNull(rng);

        var starters = new List<Player>(StartingPositions.Length);
        var shirt = 1;
        foreach (var position in StartingPositions)
        {
            starters.Add(new Player($"{club.Club.Id}-{shirt:00}", Names.Generate(rng), shirt, position, WorkRate(rng)));
            shirt++;
        }

        var bench = new List<Player>(BenchPositions.Length);
        foreach (var position in BenchPositions)
        {
            bench.Add(new Player($"{club.Club.Id}-{shirt:00}", Names.Generate(rng), shirt, position, WorkRate(rng)));
            shirt++;
        }

        return new TeamSheet(club.Club, "4-3-3", starters, bench);
    }

    /// <summary>Metres per minute on the ball-less run, uniform in [95, 125), one decimal.</summary>
    private static double WorkRate(Pcg32 rng) => Math.Round(rng.Range(95, 125), 1);
}
