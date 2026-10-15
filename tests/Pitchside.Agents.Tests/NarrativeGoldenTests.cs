using System.Text.Json;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

/// <summary>
/// With the scripted provider, seed 7's full narrative set (every persona, subject and language) is a golden master
/// (WP07-R7), one compact JSON narrative per line in id order. A prompt or checker change that alters it regenerates it
/// in the same pull request, with a reason, by running this test with <c>PITCHSIDE_UPDATE_GOLDEN=1</c>.
/// </summary>
public sealed class NarrativeGoldenTests
{
    private static readonly string Golden = RepositoryRoot.Combine("tests", "Pitchside.Agents.Tests", "golden", "seed-7.narratives.jsonl");

    [Fact]
    public async Task Seed_7_narratives_equal_the_golden_master()
    {
        var workflow = Crew.FullWorkflow(time: new FixedTimeProvider(new DateTimeOffset(2026, 10, 25, 15, 0, 0, TimeSpan.Zero)));
        var narratives = new List<Narrative>();
        foreach (var detected in Matches.Moments(7))
        {
            narratives.AddRange((await workflow.RunAsync(detected.WorkItem, CancellationToken.None)).Narratives);
        }

        var lines = narratives.OrderBy(n => n.Id, StringComparer.Ordinal).Select(n => JsonSerializer.Serialize(n, ContractJson.Options)).ToList();
        if (Environment.GetEnvironmentVariable("PITCHSIDE_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Golden)!);
            await File.WriteAllLinesAsync(Golden, lines);
        }

        lines.Count.ShouldBe(752);
        lines.ShouldBe(File.ReadAllLines(Golden).Where(line => line.Length > 0).ToList());
    }
}
