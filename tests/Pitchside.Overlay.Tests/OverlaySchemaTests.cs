using System.Text.Json;
using Json.Schema;
using Pitchside.Contracts;

namespace Pitchside.Overlay.Tests;

public sealed class OverlaySchemaTests
{
    [Fact]
    public void Every_scheduled_entry_validates_and_round_trips()
    {
        var schema = JsonSchema.FromText(File.ReadAllText(RepositoryRoot.Combine("schemas", "overlay-entry.schema.json")));
        var book = new OverlayBook();
        foreach (var (moment, sheet) in Fixture.Moments)
        {
            book.Schedule(moment, Fixture.NarrativeFor(moment, 12), sheet, Fixture.Document);
        }

        var entries = book.Entries("MER-7");
        entries.Count.ShouldBe(Fixture.Moments.Count);
        foreach (var entry in entries)
        {
            var json = JsonSerializer.Serialize(entry, ContractJson.Options);
            using var document = JsonDocument.Parse(json);
            var result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            result.IsValid.ShouldBeTrue(json);
            JsonSerializer.Serialize(JsonSerializer.Deserialize<OverlayEntry>(json, ContractJson.Options), ContractJson.Options).ShouldBe(json);
        }
    }
}
