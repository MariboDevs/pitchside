using Pitchside.Contracts;

namespace Pitchside.Evaluation.Tests;

public sealed class EvaluationReportTests
{
    private static AuditRecord Call(string stage, long latencyMs, long tokensIn, long tokensOut) => new()
    {
        Id = $"01J{stage.Length}{latencyMs:00000000000000000000000}"[..26],
        MatchId = "MER-7",
        MomentId = Items.Moment.Id,
        Stage = stage,
        Provider = "scripted",
        Model = "scripted",
        PromptVersion = $"{stage}-v1",
        PromptHash = new string('a', 64),
        InputTokens = tokensIn,
        OutputTokens = tokensOut,
        LatencyMs = latencyMs,
        Outcome = AuditOutcome.Ok,
        RequestId = string.Empty,
        Utc = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void The_report_computes_every_figure_from_the_narratives_and_the_calls()
    {
        var verified = Items.With(new NarrativeSentence("[[home.xg]]", ["home.xg"])) with { Evaluation = new NarrativeEvaluation(1, 1, 0.5) };
        var fallback = verified with { Id = "MER-7:M:0120:goal:casual:en", Status = NarrativeStatus.Fallback, Evaluation = new NarrativeEvaluation(1, 2, null) };
        var explainer = Enumerable.Range(1, 20).Select(i => Call("explainer", i * 10, 1_000, 100)).ToList();

        var report = EvaluationReport.Build("MER-7", "scripted", "local", [verified, fallback], [.. explainer, Call("factcheck", 5, 500, 50)], new PricingOptions { InputPerMillion = 2m, OutputPerMillion = 8m });

        report.ShouldContain("| verified | 1 | 50.0 % |");
        report.ShouldContain("| fallback | 1 | 50.0 % |");
        report.ShouldContain("| Mean groundedness | 0.500 |");
        report.ShouldContain("| Mean citation coverage | 1.000 |");
        report.ShouldContain("| Mean fact density (tokens per sentence) | 1.500 |");
        report.ShouldContain("| Fallback rate | 50.0 % |");
        report.ShouldContain("| explainer | 20 | 100 | 190 |");
        report.ShouldContain("| factcheck | 1 | 5 | 5 |");
        report.ShouldContain("| Input tokens | 20500 |");
        report.ShouldContain("| Output tokens | 2050 |");
        report.ShouldContain("| Tokens per narrative | 11275.000 |");
        report.ShouldContain("| Estimated cost at the configured prices | 0.0574 |");
        report.ShouldContain("Groundedness here is the local proxy");
    }

    [Fact]
    public void Percentiles_are_nearest_rank()
    {
        EvaluationReport.Percentile([], 0.5).ShouldBe(0);
        EvaluationReport.Percentile([1, 2, 3, 4], 0.5).ShouldBe(2);
        EvaluationReport.Percentile([1, 2, 3, 4], 0.95).ShouldBe(4);
    }
}
