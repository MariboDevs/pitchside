using System.Text.Json;
using Pitchside.Contracts;

namespace Pitchside.Stats.Tests;

/// <summary>The committed seed-7 sample, read from data/samples so the stats tests do not depend on the engine.</summary>
internal static class SampleMatch
{
    private static readonly Lazy<MatchDocument> LazyDocument = new(() => JsonSerializer.Deserialize<MatchDocument>(
        File.ReadAllText(RepositoryRoot.Combine("data", "samples", "seed-7", "match.json")), ContractJson.Options)!);

    private static readonly Lazy<IReadOnlyList<MatchEvent>> LazyEvents = new(() => File
        .ReadAllLines(RepositoryRoot.Combine("data", "samples", "seed-7", "events.jsonl"))
        .Where(line => line.Length > 0)
        .Select(line => JsonSerializer.Deserialize<MatchEvent>(line, ContractJson.Options)!)
        .ToList());

    public static MatchDocument Document => LazyDocument.Value;

    public static IReadOnlyList<MatchEvent> Events => LazyEvents.Value;
}
