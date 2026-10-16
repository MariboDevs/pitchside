using Azure.Core;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Pitchside.Agents;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Engine.Calibration;
using Pitchside.Evaluation;
using Pitchside.Moments;
using Pitchside.Moments.Demo;
using Pitchside.Runtime.Audit;
using Pitchside.Runtime.MomentSink;
using Pitchside.Stats;

namespace Pitchside.Cli;

/// <summary>Argument parsing and dispatch. Kept dependency-free on purpose.</summary>
public static partial class CommandLine
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, IFormatProvider culture)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        if (args.Length == 0)
        {
            await output.WriteLineAsync(Usage).ConfigureAwait(false);
            return 1;
        }

        var options = ParseOptions(args.Skip(1));
        if (options.TryGetValue("seed", out var rawSeed) && (!long.TryParse(rawSeed, NumberStyles.None, culture, out var parsedSeed) || parsedSeed <= 0))
        {
            // Match ids are MER-{seed} and the contracts require a positive integer.
            await output.WriteLineAsync($"--seed must be a positive whole number, not '{rawSeed}'.").ConfigureAwait(false);
            return 1;
        }

        switch (args[0])
        {
            case "simulate":
                return await SimulateAsync(options, output, culture).ConfigureAwait(false);
            case "calibrate":
                return await CalibrateAsync(options, output, culture).ConfigureAwait(false);
            case "facts":
                return await FactsAsync(options, output, culture).ConfigureAwait(false);
            case "moments":
                return await MomentsAsync(options, output, culture).ConfigureAwait(false);
            case "narrate":
                return await NarrateAsync(options, output, culture).ConfigureAwait(false);
            case "evaluate":
                return await EvaluateAsync(options, output, culture).ConfigureAwait(false);
            case "demo-seed":
                return await DemoSeedAsync(options, output, culture).ConfigureAwait(false);
            default:
                await output.WriteLineAsync(Usage).ConfigureAwait(false);
                return 1;
        }
    }

    private const string Usage = """
        Pitchside engine

          simulate  --seed <n> [--out <dir>]          write match.json and events.jsonl for one seeded match
          calibrate [--matches <n>] [--report <file>] simulate a sample and compare it to the real-league targets
          facts     --seed <n> [--at-seq <n>]          print the fact sheet as JSON, at full time or as of an event
          moments   --seed <n>                         print every detected moment as JSON lines
          narrate   --seed <n> [--provider scripted]   run the agent crew over every moment and print each narrative
                    [--personas all|<list>] [--languages <list>]
                    [--recap [--speech scripted|azure] [--out <dir>]]  speak the full-time recap into <dir>/recap/
          evaluate  --match MER-<n> [--provider local]   narrate every persona and language, score groundedness and
                    [--agents scripted] [--report <file>] write docs/evaluation/report-<match>.md
          demo-seed [--from 1] [--to 500] [--out <file>] score seeds for drama and write docs/DEMO.md
        """;

    private static readonly JsonSerializerOptions Indented = new(ContractJson.Options) { WriteIndented = true };

    private static async Task<int> SimulateAsync(Dictionary<string, string> options, TextWriter output, IFormatProvider culture)
    {
        var seed = long.Parse(options.GetValueOrDefault("seed", "7"), culture);
        var directory = options.GetValueOrDefault("out", Path.Combine("out", $"seed-{seed}"));
        var match = MatchSimulator.Simulate(seed);
        await MatchWriter.WriteAsync(match, directory).ConfigureAwait(false);
        await output.WriteLineAsync(string.Format(
            culture,
            "{0} {1}-{2} {3}  ({4} events) -> {5}",
            match.Document.Home.Club.Name, match.HomeScore, match.AwayScore, match.Document.Away.Club.Name,
            match.Events.Count, directory)).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> CalibrateAsync(Dictionary<string, string> options, TextWriter output, IFormatProvider culture)
    {
        var count = int.Parse(options.GetValueOrDefault("matches", "2000"), culture);
        var reportPath = options.GetValueOrDefault("report", Path.Combine("docs", "calibration", "report.md"));
        var matches = new List<SimulatedMatch>(count);
        for (var seed = 1; seed <= count; seed++)
        {
            matches.Add(MatchSimulator.Simulate(seed));
        }

        var report = CalibrationReport.Compare(AggregateStats.From(matches), CalibrationTargets.LoadEmbedded());
        var markdown = report.ToMarkdown(DateTime.UtcNow.ToString("yyyy-MM-dd", culture));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        await File.WriteAllTextAsync(reportPath, markdown).ConfigureAwait(false);
        await output.WriteAsync(markdown).ConfigureAwait(false);
        return report.AllPass ? 0 : 2;
    }

    private static async Task<int> FactsAsync(Dictionary<string, string> options, TextWriter output, IFormatProvider culture)
    {
        var seed = long.Parse(options.GetValueOrDefault("seed", "7"), culture);
        var match = MatchSimulator.Simulate(seed);
        var atSeq = options.TryGetValue("at-seq", out var raw) ? int.Parse(raw, culture) : int.MaxValue;
        var engine = new MatchStatsEngine();
        var state = engine.Start(match.Document);
        foreach (var matchEvent in match.Events.TakeWhile(e => e.Seq <= atSeq))
        {
            state = engine.Apply(state, matchEvent).State;
        }

        await output.WriteLineAsync(JsonSerializer.Serialize(engine.Snapshot(state), Indented)).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> MomentsAsync(Dictionary<string, string> options, TextWriter output, IFormatProvider culture)
    {
        var seed = long.Parse(options.GetValueOrDefault("seed", "7"), culture);
        var match = MatchSimulator.Simulate(seed);
        foreach (var moment in MatchMoments.Detect(match.Document, match.Events))
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(moment, ContractJson.Options)).ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<int> NarrateAsync(Dictionary<string, string> options, TextWriter output, IFormatProvider culture)
    {
        var seed = long.Parse(options.GetValueOrDefault("seed", "7"), culture);
        var provider = options.GetValueOrDefault("provider", "scripted");
        // --recap speaks the full-time recap (WP-10) with --speech (scripted unless given) into <out>/recap/.
        var recap = options.ContainsKey("recap");
        var speech = recap ? options.GetValueOrDefault("speech", "scripted") : "None";
        var directory = options.GetValueOrDefault("out", Path.Combine("out", $"seed-{seed}"));
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:Provider"] = provider, ["Speech:Provider"] = speech })
            .Build();
        // Without --personas and --languages the command writes the base English narrative only, as in WP-04. The
        // recap reads the analyst variant in every language, so --recap writes those unless told otherwise.
        string[] personas = options.GetValueOrDefault("personas") switch
        {
            null => recap ? [Personas.Analyst] : [],
            "all" => [.. Personas.Viewer],
            var list => list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        };
        string[] languages = options.TryGetValue("languages", out var languageList)
            ? languageList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : recap ? [.. Languages.All] : [Languages.English];
        var everyVariant = options.ContainsKey("personas") || options.ContainsKey("languages");
        await using var services = new ServiceCollection()
            .AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning).AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace))
            .AddPitchsideAgents(configuration)
            .Configure<AgentsOptions>(agents =>
            {
                agents.Personas = personas;
                agents.Languages = languages;
            })
            .BuildServiceProvider();
        var workflow = services.GetRequiredService<MomentWorkflow>();

        var match = MatchSimulator.Simulate(seed);
        var stats = new MatchStatsEngine();
        var detector = new MomentDetector();
        var statsState = stats.Start(match.Document);
        var detectorState = detector.Start(match.Document);
        var unresolved = 0;
        foreach (var matchEvent in match.Events)
        {
            var step = stats.Apply(statsState, matchEvent);
            statsState = step.State;
            var sheet = stats.Snapshot(statsState);
            var (next, moments) = detector.Apply(detectorState, matchEvent, sheet, step.Tags);
            detectorState = next;
            foreach (var moment in moments)
            {
                var context = await workflow.RunAsync(new MomentWorkItem(moment, sheet, match.Document), CancellationToken.None).ConfigureAwait(false);
                var narrative = context.BaseNarrative!;
                unresolved += narrative.Status == NarrativeStatus.Rejected ? 1 : 0;
                foreach (var spoken in context.Recaps)
                {
                    var path = Path.Combine(directory, "recap", $"{spoken.Recap.Language}.mp3");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllBytesAsync(path, spoken.Audio).ConfigureAwait(false);
                    await output.WriteLineAsync(string.Format(
                        culture,
                        "recap {0} voice={1}{2} -> {3}",
                        spoken.Recap.Language, spoken.Recap.Voice, spoken.Recap.EnglishFallback ? " (English stands in)" : string.Empty, path)).ConfigureAwait(false);
                }

                if (!everyVariant)
                {
                    await output.WriteLineAsync(string.Format(
                        culture,
                        "{0,-34} {1,-9} attempts={2}  {3}",
                        moment.Id, narrative.Status.ToString().ToLowerInvariant(), context.Attempts, narrative.RenderedText)).ConfigureAwait(false);
                    continue;
                }

                foreach (var variant in context.Narratives)
                {
                    await output.WriteLineAsync(string.Format(
                        culture,
                        "{0,-60} {1,-9} {2}",
                        variant.Id, variant.Status.ToString().ToLowerInvariant(), variant.RenderedText)).ConfigureAwait(false);
                }
            }
        }

        return unresolved == 0 ? 0 : 3;
    }

    /// <summary>
    /// The demo seed (WP-11): every seed in the range simulated and scored for drama by <see cref="DemoSeedScorer"/>,
    /// the best one written to <c>docs/DEMO.md</c> with its moment list.
    /// </summary>
    private static async Task<int> DemoSeedAsync(Dictionary<string, string> options, TextWriter output, IFormatProvider culture)
    {
        var from = long.Parse(options.GetValueOrDefault("from", "1"), culture);
        var to = long.Parse(options.GetValueOrDefault("to", "500"), culture);
        var path = options.GetValueOrDefault("out", Path.Combine("docs", "DEMO.md"));
        if (from <= 0 || to < from)
        {
            await output.WriteLineAsync("--from must be positive and --to at least --from.").ConfigureAwait(false);
            return 1;
        }

        var scores = new List<DemoSeedScore>();
        foreach (var seed in DemoSeedScorer.Seeds(from, to))
        {
            var simulated = MatchSimulator.Simulate(seed);
            scores.Add(DemoSeedScorer.Score(seed, simulated.Document, simulated.Events, MatchMoments.Detect(simulated.Document, simulated.Events)));
        }

        var best = DemoSeedScorer.Best(scores);
        var match = MatchSimulator.Simulate(best.Seed);
        var moments = MatchMoments.Detect(match.Document, match.Events);
        var markdown = DemoMarkdown(best, match, moments, scores, from, to, culture);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, markdown).ConfigureAwait(false);
        await output.WriteLineAsync(string.Format(
            culture,
            "seed {0} scores {1}/5: {2} {3}-{4} {5}, {6} moments -> {7}",
            best.Seed, best.Total, match.Document.Home.Club.Name, match.HomeScore, match.AwayScore, match.Document.Away.Club.Name, moments.Count, path)).ConfigureAwait(false);
        return 0;
    }

    private static string DemoMarkdown(
        DemoSeedScore best, SimulatedMatch match, IReadOnlyList<Moment> moments, List<DemoSeedScore> scores, long from, long to, IFormatProvider culture)
    {
        static string Mark(bool yes) => yes ? "yes" : "no";
        var text = new System.Text.StringBuilder();
        text.AppendLine("# Demo match")
            .AppendLine()
            .AppendLine(string.Format(culture, "Seed **{0}**, match `{1}`: {2} {3}, {4} {5}. Chosen by", best.Seed, match.Document.MatchId, match.Document.Home.Club.Name, match.HomeScore, match.Document.Away.Club.Name, match.AwayScore))
            .AppendLine(string.Format(culture, "`dotnet run --project src/Pitchside.Cli -- demo-seed --from {0} --to {1}`, which simulates every seed in", from, to))
            .AppendLine("the range and scores it one point each for three to five goals, a lead that changes hands, a red card or a")
            .AppendLine("penalty, a big chance missed, and a momentum swing in each half. The highest total wins and the lowest seed")
            .AppendLine("breaks a tie, so the choice is the same on every machine. This file is generated; run the command again rather")
            .AppendLine("than editing it.")
            .AppendLine()
            .AppendLine("| Criterion | Seed " + best.Seed.ToString(culture) + " |")
            .AppendLine("| --- | --- |")
            .AppendLine(string.Format(culture, "| Three to five goals | {0} ({1}) |", Mark(best.GoalsInRange), best.Goals))
            .AppendLine(string.Format(culture, "| The lead changes hands | {0} |", Mark(best.Comeback)))
            .AppendLine(string.Format(culture, "| A red card or a penalty | {0} |", Mark(best.RedCardOrPenalty)))
            .AppendLine(string.Format(culture, "| A big chance missed | {0} |", Mark(best.BigChanceMissed)))
            .AppendLine(string.Format(culture, "| A momentum swing in each half | {0} |", Mark(best.MomentumSwingEachHalf)))
            .AppendLine(string.Format(culture, "| Total | {0} of 5 |", best.Total))
            .AppendLine()
            .AppendLine(string.Format(culture, "Of the {0} seeds scored, {1} reach the same total.", scores.Count, scores.Count(s => s.Total == best.Total)))
            .AppendLine()
            .AppendLine("## Moments")
            .AppendLine()
            .AppendLine("| Clock | Type | Headline |")
            .AppendLine("| --- | --- | --- |");
        foreach (var moment in moments)
        {
            text.AppendLine(string.Format(culture, "| {0} | `{1}` | {2} |", Clock(moment.Period, moment.ClockMs, culture), moment.Type, moment.Headline.Replace("|", "/", StringComparison.Ordinal)));
        }

        return text.ToString();
    }

    private static string Clock(int period, int clockMs, IFormatProvider culture)
    {
        var minutes = clockMs / 60_000;
        var seconds = clockMs / 1000 % 60;
        return string.Format(culture, "{0}H {1:00}:{2:00}", period, minutes, seconds);
    }

    /// <summary>
    /// The evaluation report (WP07-R3): the full crew over every moment of the match's seed, each verified narrative
    /// scored for groundedness, and the figures computed from the narratives and the audit of every model call.
    /// </summary>
    private static async Task<int> EvaluateAsync(Dictionary<string, string> options, TextWriter output, IFormatProvider culture)
    {
        var matchId = options.GetValueOrDefault("match", "MER-7");
        if (!matchId.StartsWith("MER-", StringComparison.Ordinal) || !long.TryParse(matchId[4..], NumberStyles.None, culture, out var seed) || seed <= 0)
        {
            await output.WriteLineAsync($"--match must be MER-<seed>, not '{matchId}'.").ConfigureAwait(false);
            return 1;
        }

        // The simulation and the audit use the canonical id, so MER-007 reads as MER-7.
        matchId = MatchSimulator.MatchIdFor(seed);
        var evaluation = options.GetValueOrDefault("provider", EvaluationOptions.LocalProvider);
        var agents = options.GetValueOrDefault("agents", AgentsOptions.ScriptedProvider);
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:Provider"] = agents,
                ["Evaluation:Provider"] = evaluation,
            })
            .Build();
        var collection = new ServiceCollection()
            .AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning).AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace))
            .AddPitchsideAgents(configuration)
            .AddPitchsideEvaluation(configuration)
            .Configure<PricingOptions>(configuration.GetSection(PricingOptions.SectionName));
        if (string.Equals(evaluation, EvaluationOptions.FoundryProvider, StringComparison.OrdinalIgnoreCase))
        {
            // The judge shares the crew's cached credential when the crew is on Foundry too; otherwise it gets its own.
            collection.AddKeyedSingleton<IChatClient>(EvaluationServiceCollectionExtensions.JudgeKey, (sp, _) =>
                new FoundryChatProvider(
                        sp.GetRequiredService<IOptions<FoundryOptions>>(),
                        sp.GetService<TokenCredential>() ?? new CachingTokenCredential(new DefaultAzureCredential()))
                    .ChatClientFor(sp.GetRequiredService<IOptions<EvaluationOptions>>().Value.Deployment));
        }

        await using var services = collection.BuildServiceProvider();
        var workflow = services.GetRequiredService<MomentWorkflow>();
        var scorer = services.GetRequiredService<IEvaluationProvider>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Pitchside.Cli.Evaluate");
        var narratives = new List<Narrative>();
        foreach (var (moment, sheet, document) in DetectedMoments(seed))
        {
            var context = await workflow.RunAsync(new MomentWorkItem(moment, sheet, document), CancellationToken.None).ConfigureAwait(false);
            foreach (var narrative in context.Narratives)
            {
                double? score = null;
                if (narrative.Status == NarrativeStatus.Verified)
                {
                    try
                    {
                        score = await scorer.ScoreAsync(new EvaluationItem(narrative, moment, sheet, document), CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ProviderFailure.IsRecoverable(ex))
                    {
                        // An unscored narrative is reported as such; a judge fault never loses the report.
                        LogJudgeFailed(logger, narrative.Id, ex.GetType().Name, ex.Message);
                    }
                }

                narratives.Add(narrative with { Evaluation = narrative.Evaluation with { Groundedness = score } });
            }
        }

        var audit = await services.GetRequiredService<IAuditStore>().ListAsync(matchId, null, int.MaxValue, CancellationToken.None).ConfigureAwait(false);
        var markdown = EvaluationReport.Build(matchId, agents.ToLowerInvariant(), scorer.Name, narratives, audit, services.GetRequiredService<IOptions<PricingOptions>>().Value);
        var path = options.GetValueOrDefault("report", Path.Combine("docs", "evaluation", $"report-{matchId}.md"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, markdown).ConfigureAwait(false);
        await output.WriteAsync(markdown).ConfigureAwait(false);
        await output.WriteLineAsync($"-> {path}").ConfigureAwait(false);
        return 0;
    }

    /// <summary>Every moment of a seeded match with the fact sheet as of its trigger, in detection order.</summary>
    private static IEnumerable<(Moment Moment, FactSheet Sheet, MatchDocument Document)> DetectedMoments(long seed)
    {
        var match = MatchSimulator.Simulate(seed);
        var stats = new MatchStatsEngine();
        var detector = new MomentDetector();
        var statsState = stats.Start(match.Document);
        var detectorState = detector.Start(match.Document);
        foreach (var matchEvent in match.Events)
        {
            var step = stats.Apply(statsState, matchEvent);
            statsState = step.State;
            var sheet = stats.Snapshot(statsState);
            var (next, moments) = detector.Apply(detectorState, matchEvent, sheet, step.Tags);
            detectorState = next;
            foreach (var moment in moments)
            {
                yield return (moment, sheet, match.Document);
            }
        }
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        string? key = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                key = arg[2..];
                options[key] = "true";
            }
            else if (key is not null)
            {
                options[key] = arg;
                key = null;
            }
        }

        return options;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Narrative {NarrativeId}: groundedness judge failed ({Kind}): {Detail}; left unscored")]
    private static partial void LogJudgeFailed(ILogger logger, string narrativeId, string kind, string detail);
}
