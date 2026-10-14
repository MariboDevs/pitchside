using System.Text.Json;
using Json.Schema;
using Pitchside.Contracts;
using Pitchside.Engine;

namespace Pitchside.Engine.Tests;

/// <summary>The committed sample and freshly simulated matches validate against both JSON Schemas.</summary>
public sealed class SchemaValidationTests
{
    private static readonly JsonSchema EventSchema = Load("event.schema.json");
    private static readonly JsonSchema MatchSchema = Load("match.schema.json");

    [Fact]
    public void Sample_match_document_validates()
    {
        var text = File.ReadAllText(RepositoryRoot.Combine("data", "samples", "seed-7", "match.json"));
        AssertValid(MatchSchema, text);
    }

    [Fact]
    public void Every_sample_event_validates()
    {
        var lines = File.ReadAllLines(RepositoryRoot.Combine("data", "samples", "seed-7", "events.jsonl"));
        lines.Length.ShouldBeGreaterThan(0);
        foreach (var line in lines)
        {
            AssertValid(EventSchema, line);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20261027)]
    public void Simulated_matches_validate(long seed)
    {
        var match = MatchSimulator.Simulate(seed);
        AssertValid(MatchSchema, JsonSerializer.Serialize(match.Document, ContractJson.Options));
        foreach (var e in match.Events)
        {
            AssertValid(EventSchema, JsonSerializer.Serialize(e, ContractJson.Options));
        }
    }

    [Fact]
    public void Sample_carries_the_current_schema_version()
    {
        var text = File.ReadAllText(RepositoryRoot.Combine("data", "samples", "seed-7", "match.json"));
        using var document = JsonDocument.Parse(text);
        document.RootElement.GetProperty("schemaVersion").GetString().ShouldBe(SchemaVersion.Current);
    }

    private static JsonSchema Load(string file) =>
        JsonSchema.FromText(File.ReadAllText(RepositoryRoot.Combine("schemas", file)));

    private static void AssertValid(JsonSchema schema, string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        var errors = (result.Details ?? [])
            .Where(d => d.Errors is not null)
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .ToList();
        result.IsValid.ShouldBeTrue($"{json}{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }
}
