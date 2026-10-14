using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pitchside.Agents;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Engine.Calibration;
using Pitchside.Moments;
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
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:Provider"] = provider })
            .AddEnvironmentVariables()
            .Build();
        await using var services = new ServiceCollection()
            .AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .AddPitchsideAgents(configuration)
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
                await output.WriteLineAsync(string.Format(
                    culture,
                    "{0,-34} {1,-9} attempts={2}  {3}",
                    moment.Id, narrative.Status.ToString().ToLowerInvariant(), context.Attempts, narrative.RenderedText)).ConfigureAwait(false);
            }
        }

        return unresolved == 0 ? 0 : 3;
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
