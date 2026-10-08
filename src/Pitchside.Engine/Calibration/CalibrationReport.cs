using System.Globalization;
using System.Text;

namespace Pitchside.Engine.Calibration;

/// <summary>Synthetic aggregates beside the real targets, with a tolerance and a verdict per statistic.</summary>
public sealed class CalibrationReport
{
    private static readonly (string Statistic, double Tolerance)[] Checks =
    [
        ("goals_per_match", 0.15),
        ("home_goals", 0.12),
        ("away_goals", 0.12),
        ("p_home", 0.03),
        ("p_draw", 0.03),
        ("p_away", 0.03),
        ("p_over25", 0.04),
        ("p_btts", 0.04),
        ("p_0_0", 0.02),
        ("first_half_goal_share", 0.03),
        ("home_shots", 1.0),
        ("away_shots", 1.0),
        ("home_sot", 0.5),
        ("away_sot", 0.5),
        ("goals_per_shot", 0.015),
        ("goals_per_sot", 0.04),
        ("home_corners", 0.6),
        ("away_corners", 0.6),
        ("home_fouls", 1.2),
        ("away_fouls", 1.2),
        ("home_yellows", 0.3),
        ("away_yellows", 0.3),
        ("home_reds", 0.03),
        ("away_reds", 0.03),
    ];

    private CalibrationReport(AggregateStats synthetic, CalibrationTargets targets, IReadOnlyList<CalibrationRow> rows)
    {
        Synthetic = synthetic;
        Targets = targets;
        Rows = rows;
    }

    public AggregateStats Synthetic { get; }

    public CalibrationTargets Targets { get; }

    public IReadOnlyList<CalibrationRow> Rows { get; }

    public bool AllPass => Rows.All(r => r.Passes);

    public int Failures => Rows.Count(r => !r.Passes);

    public static CalibrationReport Compare(AggregateStats synthetic, CalibrationTargets targets)
    {
        ArgumentNullException.ThrowIfNull(synthetic);
        ArgumentNullException.ThrowIfNull(targets);
        var rows = Checks
            .Select(c => new CalibrationRow(c.Statistic, synthetic[c.Statistic], targets[c.Statistic], c.Tolerance))
            .ToList();
        return new CalibrationReport(synthetic, targets, rows);
    }

    public string ToMarkdown(string generatedOn)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Calibration report");
        sb.AppendLine();
        sb.Append("Generated ").Append(generatedOn).Append(" from ")
            .Append(Synthetic.Matches.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" simulated matches, compared to target set `").Append(Targets.Key).AppendLine("`.");
        sb.AppendLine();
        sb.Append("Result: **").Append(AllPass ? "all statistics within tolerance" : $"{Failures} of {Rows.Count} statistics outside tolerance").AppendLine("**.");
        sb.AppendLine();
        sb.AppendLine("| Statistic | Synthetic | Target | Gap | Tolerance | Verdict |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");
        foreach (var row in Rows)
        {
            sb.Append("| ").Append(row.Statistic)
              .Append(" | ").Append(row.Synthetic.ToString("0.000", CultureInfo.InvariantCulture))
              .Append(" | ").Append(row.Target.ToString("0.000", CultureInfo.InvariantCulture))
              .Append(" | ").Append(row.Gap.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture))
              .Append(" | ").Append(row.Tolerance.ToString("0.000", CultureInfo.InvariantCulture))
              .Append(" | ").Append(row.Passes ? "pass" : "FAIL").AppendLine(" |");
        }

        sb.AppendLine();
        sb.AppendLine("Targets are league-level averages from football-data.co.uk (English top flight, 2015/16 to 2025/26). See docs/CALIBRATION.md.");
        return sb.ToString();
    }
}
