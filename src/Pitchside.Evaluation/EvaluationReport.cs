using System.Globalization;
using System.Text;
using Pitchside.Contracts;

namespace Pitchside.Evaluation;

/// <summary>
/// The evaluation report for one match (WP07-R3), in Markdown: narratives by status, groundedness, citation
/// coverage, fallback rate, latency per stage at p50 and p95, tokens per narrative and a cost estimate from the
/// configured prices. Every figure is computed here from the narratives and the audit records.
/// </summary>
public static class EvaluationReport
{
    public static string Build(
        string matchId,
        string agentsProvider,
        string evaluationProvider,
        IReadOnlyList<Narrative> narratives,
        IReadOnlyList<AuditRecord> audit,
        PricingOptions pricing)
    {
        ArgumentNullException.ThrowIfNull(narratives);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(pricing);
        var c = CultureInfo.InvariantCulture;
        var report = new StringBuilder();
        report.Append(c, $"# Evaluation report: {matchId}\n\n");
        report.Append(c, $"Agents provider `{agentsProvider}`, evaluation provider `{evaluationProvider}`. {narratives.Count} narratives from {narratives.Select(n => n.MomentId).Distinct().Count()} moments; {audit.Count} model calls.\n\n");

        report.Append("## Narratives by status\n\n| Status | Count | Share |\n| --- | ---: | ---: |\n");
        foreach (var status in Enum.GetValues<NarrativeStatus>())
        {
            var count = narratives.Count(n => n.Status == status);
            report.Append(c, $"| {status.ToString().ToLowerInvariant()} | {count} | {Share(count, narratives.Count)} |\n");
        }

        var scored = narratives.Where(n => n.Evaluation.Groundedness is not null).Select(n => n.Evaluation.Groundedness!.Value).ToList();
        var fallbacks = narratives.Count(n => n.Status == NarrativeStatus.Fallback);
        report.Append("\n## Quality\n\n| Measure | Value |\n| --- | ---: |\n");
        report.Append(c, $"| Narratives scored for groundedness | {scored.Count} |\n");
        report.Append(c, $"| Mean groundedness | {Number(scored.Count == 0 ? null : scored.Average())} |\n");
        report.Append(c, $"| Minimum groundedness | {Number(scored.Count == 0 ? null : scored.Min())} |\n");
        report.Append(c, $"| Mean citation coverage | {Number(narratives.Count == 0 ? null : narratives.Average(n => n.Evaluation.CitationCoverage))} |\n");
        report.Append(c, $"| Mean fact density (tokens per sentence) | {Number(narratives.Count == 0 ? null : narratives.Average(n => n.Evaluation.FactDensity))} |\n");
        report.Append(c, $"| Fallback rate | {Share(fallbacks, narratives.Count)} |\n");
        if (string.Equals(evaluationProvider, LocalEvaluationProvider.ProviderName, StringComparison.Ordinal))
        {
            report.Append("\nGroundedness here is the local proxy: the share of a narrative's sentences that cite only the moment's\n")
                .Append("evidence keys. The crew may also cite `match.*` facts and the side's and players' facts, so this measures how\n")
                .Append("closely a narrative keeps to its moment, not whether its numbers are true; citation coverage shows that every\n")
                .Append("number resolves. The Foundry judge scores groundedness against every fact the narrative cites.\n");
        }

        report.Append("\n## Latency per stage\n\n| Stage | Calls | p50 ms | p95 ms |\n| --- | ---: | ---: | ---: |\n");
        foreach (var stage in audit.GroupBy(r => r.Stage, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var latencies = stage.Select(r => r.LatencyMs).Order().ToList();
            report.Append(c, $"| {stage.Key} | {latencies.Count} | {Percentile(latencies, 0.50)} | {Percentile(latencies, 0.95)} |\n");
        }

        var input = audit.Sum(r => r.InputTokens);
        var output = audit.Sum(r => r.OutputTokens);
        var cost = (input * pricing.InputPerMillion / 1_000_000m) + (output * pricing.OutputPerMillion / 1_000_000m);
        report.Append("\n## Tokens and cost\n\n| Measure | Value |\n| --- | ---: |\n");
        report.Append(c, $"| Input tokens | {input} |\n");
        report.Append(c, $"| Output tokens | {output} |\n");
        report.Append(c, $"| Tokens per narrative | {Number(narratives.Count == 0 ? null : (double)(input + output) / narratives.Count)} |\n");
        report.Append(c, $"| Estimated cost at the configured prices | {cost.ToString("F4", c)} |\n");
        return report.ToString();
    }

    /// <summary>The nearest-rank percentile of an ascending list; 0 for an empty one.</summary>
    public static long Percentile(IReadOnlyList<long> ascending, double percentile)
    {
        ArgumentNullException.ThrowIfNull(ascending);
        if (ascending.Count == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile * ascending.Count);
        return ascending[Math.Clamp(rank, 1, ascending.Count) - 1];
    }

    private static string Number(double? value) => value is { } v ? v.ToString("F3", CultureInfo.InvariantCulture) : "n/a";

    private static string Share(int count, int total) =>
        total == 0 ? "n/a" : ((double)count / total).ToString("P1", CultureInfo.InvariantCulture);
}
