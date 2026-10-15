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
using Pitchside.Runtime.Audit;
using Pitchside.Runtime.MomentSink;
using Pitchside.Stats;

namespace Pitchside.Cli;

/// <summary>Argument parsing and dispatch. Kept dependency-free on purpose.</summary>
public static class CommandLine
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
          evaluate  --match MER-<n> [--provider local]   narrate every persona and language, score groundedness and
                    [--agents scripted] [--report <file>] write docs/evaluation/report-<match>.md
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
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:Provider"] = provider })
            .Build();
        // Without --personas and --languages the command writes the base English narrative only, as in WP-04.
        string[] personas = options.GetValueOrDefault("personas") switch
        {
            null => [],
            "all" => [.. Personas.Viewer],
            var list => list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        };
        string[] languages = options.TryGetValue("languages", out var languageList)
            ? languageList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [Languages.English];
        var everyVariant = options.ContainsKey("personas") || options.ContainsKey("languages");
        await using var services = new ServiceCollection()
            .AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
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
            .AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .AddPitchsideAgents(configuration)
            .AddPitchsideEvaluation(configuration)
            .Configure<PricingOptions>(configuration.GetSection(PricingOptions.SectionName));
        if (string.Equals(evaluation, EvaluationOptions.FoundryProvider, StringComparison.OrdinalIgnoreCase))
        {
            collection.AddKeyedSingleton<IChatClient>(EvaluationServiceCollectionExtensions.JudgeKey, (sp, _) =>
                new FoundryChatProvider(sp.GetRequiredService<IOptions<FoundryOptions>>(), new DefaultAzureCredential())
                    .ChatClientFor(sp.GetRequiredService<IOptions<EvaluationOptions>>().Value.Deployment));
        }

        await using var services = collection.BuildServiceProvider();
        var workflow = services.GetRequiredService<MomentWorkflow>();
        var scorer = services.GetRequiredService<IEvaluationProvider>();
        var narratives = new List<Narrative>();
        foreach (var (moment, sheet, document) in DetectedMoments(seed))
        {
            var context = await workflow.RunAsync(new MomentWorkItem(moment, sheet, document), CancellationToken.None).ConfigureAwait(false);
            foreach (var narrative in context.Narratives)
            {
                var score = narrative.Status == NarrativeStatus.Verified
                    ? await scorer.ScoreAsync(new EvaluationItem(narrative, moment, sheet), CancellationToken.None).ConfigureAwait(false)
                    : null;
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
}
