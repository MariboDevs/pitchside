using System.Text.Json;
using Json.Schema;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

public sealed class NarrativeSchemaTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromText(File.ReadAllText(RepositoryRoot.Combine("schemas", "narrative.schema.json")));

    [Fact]
    public async Task Every_seed_7_narrative_validates_and_round_trips()
    {
        // The full crew, so every persona variant with its subject and every language is validated too.
        var workflow = Crew.FullWorkflow();
        foreach (var detected in Matches.Moments(7))
        {
            var context = await workflow.RunAsync(detected.WorkItem, CancellationToken.None);
            foreach (var narrative in context.Narratives)
            {
                var json = JsonSerializer.Serialize(narrative, ContractJson.Options);
                using var document = JsonDocument.Parse(json);
                var result = Schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
                var errors = (result.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(x => $"{d.InstanceLocation}: {x.Value}"));
                result.IsValid.ShouldBeTrue(json + Environment.NewLine + string.Join(Environment.NewLine, errors));
                JsonSerializer.Serialize(JsonSerializer.Deserialize<Narrative>(json, ContractJson.Options), ContractJson.Options).ShouldBe(json);
            }
        }
    }
}
