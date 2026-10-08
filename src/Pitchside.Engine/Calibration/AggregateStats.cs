using Pitchside.Contracts;

namespace Pitchside.Engine.Calibration;

/// <summary>Per-match averages over a sample of simulated matches, named to match the targets file.</summary>
public sealed class AggregateStats
{
    private readonly Dictionary<string, double> _values;

    private AggregateStats(int matches, Dictionary<string, double> values)
    {
        Matches = matches;
        _values = values;
    }

    public int Matches { get; }

    public IReadOnlyDictionary<string, double> Values => _values;

    public double this[string statistic] => _values[statistic];

    public static AggregateStats From(IReadOnlyList<SimulatedMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        if (matches.Count == 0)
        {
            throw new ArgumentException("At least one match is required.", nameof(matches));
        }

        var n = (double)matches.Count;
        double homeGoals = 0, awayGoals = 0, homeWins = 0, draws = 0, awayWins = 0, over25 = 0, btts = 0, goalless = 0;
        double firstHalfGoals = 0, totalGoals = 0;
        double homeShots = 0, awayShots = 0, homeSot = 0, awaySot = 0, homeCorners = 0, awayCorners = 0;
        double homeFouls = 0, awayFouls = 0, homeYellows = 0, awayYellows = 0, homeReds = 0, awayReds = 0;

        foreach (var match in matches)
        {
            var homeId = match.Document.Home.Club.Id;
            var hg = match.HomeScore;
            var ag = match.AwayScore;
            homeGoals += hg;
            awayGoals += ag;
            totalGoals += hg + ag;
            if (hg > ag)
            {
                homeWins++;
            }
            else if (hg == ag)
            {
                draws++;
            }
            else
            {
                awayWins++;
            }

            if (hg + ag > 2)
            {
                over25++;
            }

            if (hg > 0 && ag > 0)
            {
                btts++;
            }

            if (hg + ag == 0)
            {
                goalless++;
            }

            foreach (var e in match.Events)
            {
                var home = e.TeamId == homeId;
                switch (e.Type)
                {
                    case EventType.Goal:
                        if (e.Period == 1)
                        {
                            firstHalfGoals++;
                        }

                        break;
                    case EventType.Shot:
                        Add(ref homeShots, ref awayShots, home);
                        if (e.OnTarget == true)
                        {
                            Add(ref homeSot, ref awaySot, home);
                        }

                        break;
                    case EventType.Corner:
                        Add(ref homeCorners, ref awayCorners, home);
                        break;
                    case EventType.Foul:
                        Add(ref homeFouls, ref awayFouls, home);
                        break;
                    case EventType.Card:
                        if (e.Colour == CardColour.Yellow)
                        {
                            Add(ref homeYellows, ref awayYellows, home);
                        }
                        else
                        {
                            Add(ref homeReds, ref awayReds, home);
                            if (e.SecondYellow == true)
                            {
                                Add(ref homeYellows, ref awayYellows, home);
                            }
                        }

                        break;
                    default:
                        break;
                }
            }
        }

        var values = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["goals_per_match"] = (homeGoals + awayGoals) / n,
            ["home_goals"] = homeGoals / n,
            ["away_goals"] = awayGoals / n,
            ["p_home"] = homeWins / n,
            ["p_draw"] = draws / n,
            ["p_away"] = awayWins / n,
            ["p_over25"] = over25 / n,
            ["p_btts"] = btts / n,
            ["p_0_0"] = goalless / n,
            ["first_half_goal_share"] = totalGoals == 0 ? 0 : firstHalfGoals / totalGoals,
            ["home_shots"] = homeShots / n,
            ["away_shots"] = awayShots / n,
            ["home_sot"] = homeSot / n,
            ["away_sot"] = awaySot / n,
            ["goals_per_shot"] = (homeShots + awayShots) == 0 ? 0 : totalGoals / (homeShots + awayShots),
            ["goals_per_sot"] = (homeSot + awaySot) == 0 ? 0 : totalGoals / (homeSot + awaySot),
            ["home_corners"] = homeCorners / n,
            ["away_corners"] = awayCorners / n,
            ["home_fouls"] = homeFouls / n,
            ["away_fouls"] = awayFouls / n,
            ["home_yellows"] = homeYellows / n,
            ["away_yellows"] = awayYellows / n,
            ["home_reds"] = homeReds / n,
            ["away_reds"] = awayReds / n,
        };
        return new AggregateStats(matches.Count, values);
    }

    private static void Add(ref double home, ref double away, bool isHome)
    {
        if (isHome)
        {
            home++;
        }
        else
        {
            away++;
        }
    }
}
