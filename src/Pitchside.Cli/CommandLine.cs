using System.Globalization;
using Pitchside.Engine;
using Pitchside.Engine.Calibration;

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
        switch (args[0])
        {
            case "simulate":
                return await SimulateAsync(options, output, culture).ConfigureAwait(false);
            case "calibrate":
                return await CalibrateAsync(options, output, culture).ConfigureAwait(false);
            default:
                await output.WriteLineAsync(Usage).ConfigureAwait(false);
                return 1;
        }
    }

    private const string Usage = """
        Pitchside engine

          simulate  --seed <n> [--out <dir>]          write match.json and events.jsonl for one seeded match
          calibrate [--matches <n>] [--report <file>] simulate a sample and compare it to the real-league targets
        """;

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
