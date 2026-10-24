using System.Text.Json;

namespace Pitchside.Fabric.Tests;

/// <summary>
/// The exported Activator definition alerts once per qualifying shot: each run of the source keeps one whole clock
/// minute of rows, so each row reaches the rule once, and the one rule fires on a row whose <c>shotsInWindow</c> is at
/// the threshold. Fabric checks references only on upload, so they are checked here first.
/// </summary>
public sealed class ActivatorDefinitionTests
{
    private static readonly IReadOnlyList<JsonElement> Entities = Load("fabric/activator/ReflexEntities.json");

    private static readonly IReadOnlyDictionary<string, double> Kql = KqlFile.Constants("fabric/activator/pressure-alerts.kql");

    [Fact]
    public void Every_reference_resolves()
    {
        var ids = Entities.Select(e => e.GetProperty("uniqueIdentifier").GetString()!).ToHashSet();

        ids.Count.ShouldBe(Entities.Count);
        foreach (var entity in Entities)
        {
            var payload = entity.GetProperty("payload");
            var references = References(payload).ToList();
            if (payload.TryGetProperty("definition", out var definition) && definition.TryGetProperty("instance", out _))
            {
                references.AddRange(References(Instance(entity)));
            }

            references.ShouldAllBe(id => ids.Contains(id), payload.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void The_source_reads_each_minute_of_rows_once()
    {
        var source = Entities.Single(e => e.GetProperty("type").GetString() == "kqlSource-v1").GetProperty("payload");
        var query = source.GetProperty("query").GetProperty("queryString").GetString()!;

        source.GetProperty("runSettings").GetProperty("executionIntervalInSeconds").GetInt32().ShouldBe(60);
        query.ShouldContain("PressureAlerts(");

        // One whole clock minute per run, ending 2 minutes back: consecutive runs take consecutive minutes. The time
        // axis is off; its watermark did not move, so it returned the same row on every poll.
        query.ShouldContain("let until = bin(now(), 1m) - 2m;");
        query.ShouldContain("publishedUtc >= until - 1m and publishedUtc < until");
        (source.TryGetProperty("eventTimeSettings", out var time) && time.ValueKind != JsonValueKind.Null).ShouldBeFalse();
        source.GetProperty("queryParameters").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void The_one_rule_fires_on_a_shot_at_the_threshold()
    {
        var rule = Views("Rule").Single();
        var instance = Instance(rule);
        var rows = Rows(rule).ToList();
        var selector = rows.Single(r => r.GetProperty("kind").GetString() == "Event");
        var source = Views("Event").Single(e => Template(e) == "SourceEvent");
        var condition = rows.Single(r => r.GetProperty("kind").GetString() == "NumberValueCondition");

        Template(rule).ShouldBe("EventTrigger");
        Reference(selector, "event").ShouldBe(source.GetProperty("uniqueIdentifier").GetString());
        rule.GetProperty("payload").GetProperty("definition").GetProperty("settings").GetProperty("shouldRun").GetBoolean().ShouldBeTrue();
        instance.GetProperty("steps").EnumerateArray().Single(s => s.GetProperty("name").GetString() == "EventDetectStep")
            .GetProperty("rows").EnumerateArray().Select(r => Argument(r, r.GetProperty("kind").GetString() == "EventField" ? "fieldName" : "op"))
            .ShouldBe(["shotsInWindow", "IsGreaterThanOrEqualTo"]);
        ArgumentElement(condition, "threshold").GetDouble().ShouldBe(Kql["shotThreshold"]);
    }

    [Fact]
    public void No_rule_fires_on_every_event_or_waits_for_a_crossing()
    {
        // OnEveryValue on a source without a time axis sent one email per poll. NumberBecomes on a per-shot object never
        // fired, because each object's one value was already at the threshold.
        Views("Rule").SelectMany(Rows).Select(r => r.GetProperty("kind").GetString())
            .ShouldNotContain(kind => kind == "OnEveryValue" || kind == "NumberBecomes");
    }

    private static IEnumerable<JsonElement> Views(string type) =>
        Entities.Where(e => e.GetProperty("type").GetString() == "timeSeriesView-v1"
            && e.GetProperty("payload").GetProperty("definition").GetProperty("type").GetString() == type);

    private static JsonElement Entity(string id) => Entities.Single(e => e.GetProperty("uniqueIdentifier").GetString() == id);

    private static JsonElement Instance(JsonElement entity)
    {
        using var document = JsonDocument.Parse(entity.GetProperty("payload").GetProperty("definition").GetProperty("instance").GetString()!);
        return document.RootElement.Clone();
    }

    private static string Template(JsonElement entity) => Instance(entity).GetProperty("templateId").GetString()!;

    private static IEnumerable<JsonElement> Rows(JsonElement entity) =>
        Instance(entity).GetProperty("steps").EnumerateArray().SelectMany(s => s.GetProperty("rows").EnumerateArray());

    private static JsonElement ArgumentElement(JsonElement row, string name) =>
        row.GetProperty("arguments").EnumerateArray().Single(a => a.GetProperty("name").GetString() == name).GetProperty("value");

    private static string Argument(JsonElement row, string name) => ArgumentElement(row, name).GetString()!;

    /// <summary>The entity id a row's nested reference argument (an attribute or an event) points at.</summary>
    private static string Reference(JsonElement row, string name) =>
        References(row.GetProperty("arguments").EnumerateArray().Single(a => a.GetProperty("name").GetString() == name)).Single();

    /// <summary>Every entity id under an element: <c>entityId</c> arguments and parent references.</summary>
    private static IEnumerable<string> References(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("name", out var name) && name.GetString() == "entityId" && element.TryGetProperty("value", out var value))
                {
                    yield return value.GetString()!;
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == "targetUniqueIdentifier")
                    {
                        yield return property.Value.GetString()!;
                    }
                    else if (property.Name != "instance")
                    {
                        foreach (var id in References(property.Value))
                        {
                            yield return id;
                        }
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var id in element.EnumerateArray().SelectMany(References))
                {
                    yield return id;
                }

                break;
        }
    }

    private static List<JsonElement> Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.Combine(path.Split('/'))));
        return [.. document.RootElement.EnumerateArray().Select(e => e.Clone())];
    }
}
