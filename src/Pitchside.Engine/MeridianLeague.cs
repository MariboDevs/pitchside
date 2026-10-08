using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>
/// The fictional competition every Pitchside match belongs to. Twenty invented clubs with a deterministic
/// spread of attack and defence ratings. Nothing here refers to a real club.
/// </summary>
public static class MeridianLeague
{
    public const string Name = "Meridian League";

    private static readonly (string Name, string Short, string Primary, string Secondary)[] ClubSeeds =
    [
        ("Harbourside Athletic", "HAR", "#0B3D91", "#FFFFFF"),
        ("Northgate United", "NGU", "#C8102E", "#FFFFFF"),
        ("Kingsmead Rovers", "KMR", "#1D1D1B", "#F2C300"),
        ("Eastvale Town", "EVT", "#006A4E", "#FFFFFF"),
        ("Blackwater FC", "BWF", "#111111", "#9BD3DD"),
        ("Silverton City", "SIL", "#6CABDD", "#1C2C5B"),
        ("Meridian Wanderers", "MER", "#5B2C6F", "#F4D03F"),
        ("Ashford Albion", "ASH", "#FFFFFF", "#D71920"),
        ("Thornbury FC", "THB", "#F26522", "#FFFFFF"),
        ("Redcliffe United", "RCU", "#A50034", "#FFFFFF"),
        ("Stonebridge Rangers", "STB", "#0033A0", "#FFFFFF"),
        ("Westmoor City", "WMC", "#00A3E0", "#FFFFFF"),
        ("Greyhaven Town", "GHT", "#5A5A5A", "#F9E04B"),
        ("Oakhurst Athletic", "OAK", "#1B4D3E", "#E8C547"),
        ("Brightwater FC", "BRW", "#FFD200", "#1C1C1C"),
        ("Lindenfield United", "LFU", "#7A1F2B", "#C9A227"),
        ("Amanzi United", "AMZ", "#0077B6", "#FFB703"),
        ("Marula City", "MAR", "#2A9D8F", "#E9C46A"),
        ("Inyoni Wanderers", "INY", "#E76F51", "#264653"),
        ("Ukuthula FC", "UKU", "#8E44AD", "#F8F9FA"),
    ];

    public static int ClubCount => ClubSeeds.Length;

    /// <summary>Every club, with ratings fixed for the league so the same index is the same club on any machine.</summary>
    public static IReadOnlyList<EngineClub> Clubs { get; } = BuildClubs();

    public static EngineClub Club(int index) => Clubs[index];

    private static EngineClub[] BuildClubs()
    {
        var rng = new Pcg32(20260929UL, 7UL);
        var clubs = new EngineClub[ClubSeeds.Length];
        for (var i = 0; i < ClubSeeds.Length; i++)
        {
            var (name, shortName, primary, secondary) = ClubSeeds[i];
            // Ratings in [0.75, 1.25], roughly a top-flight spread, drawn once from the league seed.
            var attack = Math.Round(rng.Range(0.75, 1.25), 3);
            var defence = Math.Round(rng.Range(0.75, 1.25), 3);
            clubs[i] = new EngineClub(new Club(shortName, name, shortName, primary, secondary), attack, defence);
        }

        return clubs;
    }
}
