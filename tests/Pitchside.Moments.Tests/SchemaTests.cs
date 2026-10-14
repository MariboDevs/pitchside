using System.Text.Json;
using Json.Schema;
using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class SchemaTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromText(File.ReadAllText(RepositoryRoot.Combine("schemas", "moment.schema.json")));

    [Fact]
    public void Every_sample_moment_validates_and_round_trips()
    {
        foreach (var moment in MatchMoments.Detect(SampleMatch.Document, SampleMatch.Events))
        {
            var json = JsonSerializer.Serialize(moment, ContractJson.Options);
            using var document = JsonDocument.Parse(json);
            var result = Schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            var errors = (result.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(x => $"{d.InstanceLocation}: {x.Value}"));
            result.IsValid.ShouldBeTrue(json + Environment.NewLine + string.Join(Environment.NewLine, errors));

            var back = JsonSerializer.Deserialize<Moment>(json, ContractJson.Options)!;
            JsonSerializer.Serialize(back, ContractJson.Options).ShouldBe(json);
        }
    }
}
