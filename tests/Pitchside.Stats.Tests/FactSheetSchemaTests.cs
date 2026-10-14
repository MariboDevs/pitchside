using System.Text.Json;
using Json.Schema;
using Pitchside.Contracts;

namespace Pitchside.Stats.Tests;

public sealed class FactSheetSchemaTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromText(File.ReadAllText(RepositoryRoot.Combine("schemas", "fact-sheet.schema.json")));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(400)]
    [InlineData(-1)]
    public void Snapshot_validates_against_the_schema(int count)
    {
        var engine = new MatchStatsEngine();
        var state = engine.Start(SampleMatch.Document);
        var take = count < 0 ? SampleMatch.Events.Count : count;
        foreach (var e in SampleMatch.Events.Take(take))
        {
            state = engine.Apply(state, e).State;
        }

        var json = JsonSerializer.Serialize(engine.Snapshot(state), ContractJson.Options);
        using var document = JsonDocument.Parse(json);
        var result = Schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        var errors = (result.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(x => $"{d.InstanceLocation}: {x.Value}"));
        result.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void A_sheet_round_trips_through_json()
    {
        var engine = new MatchStatsEngine();
        var state = engine.Start(SampleMatch.Document);
        foreach (var e in SampleMatch.Events.Take(900))
        {
            state = engine.Apply(state, e).State;
        }

        var sheet = engine.Snapshot(state);
        var json = JsonSerializer.Serialize(sheet, ContractJson.Options);
        var back = JsonSerializer.Deserialize<FactSheet>(json, ContractJson.Options)!;
        back.ShouldEqualSheet(sheet);
        JsonSerializer.Serialize(back, ContractJson.Options).ShouldBe(json);
    }
}
