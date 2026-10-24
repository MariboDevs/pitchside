using System.Text.Json;

namespace Pitchside.Fabric.Tests;

/// <summary>
/// The exported Activator definition alerts once per qualifying shot: the source calls the committed function, a
/// Pressure spell object is keyed by the query's <c>alertId</c>, and the one rule fires when that object's
/// <c>shotsInWindow</c> becomes the threshold, not on every event. Fabric checks references only on upload, so they are
/// checked here first.
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
    public void The_source_polls_the_committed_function_every_minute()
    {
        var source = Entities.Single(e => e.GetProperty("type").GetString() == "kqlSource-v1").GetProperty("payload");

        source.GetProperty("query").GetProperty("queryString").GetString().ShouldStartWith("PressureAlerts(");
        source.GetProperty("runSettings").GetProperty("executionIntervalInSeconds").GetInt32().ShouldBe(60);
    }

    [Fact]
    public void The_object_is_keyed_by_alert_id()
    {
        var spell = ObjectId();
        var split = Views("Event").Single(e => Template(e) == "SplitEvent");
        var mapping = Rows(split).Single(r => r.GetProperty("kind").GetString() == "FieldIdMapping");
        var idPart = Entity(Reference(mapping, "idPart"));

        Parent(split).ShouldBe(spell);
        Argument(mapping, "fieldName").ShouldBe("alertId");
        Template(idPart).ShouldBe("IdentityPartAttribute");
        Parent(idPart).ShouldBe(spell);
    }

    [Fact]
    public void The_one_rule_fires_when_shots_in_window_becomes_the_threshold()
    {
        var rule = Views("Rule").Single();
        var rows = Rows(rule).ToList();
        var selector = rows.Single(r => r.GetProperty("kind").GetString() == "Attribute");
        var attribute = Entity(Reference(selector, "attribute"));
        var detect = rows.Single(r => r.GetProperty("kind").GetString() == "NumberBecomes");

        Template(rule).ShouldBe("AttributeTrigger");
        Parent(rule).ShouldBe(ObjectId());
        rule.GetProperty("payload").GetProperty("definition").GetProperty("settings").GetProperty("shouldRun").GetBoolean().ShouldBeTrue();
        Parent(attribute).ShouldBe(ObjectId());
        Template(attribute).ShouldBe("BasicEventAttribute");
        Argument(Rows(attribute).Single(r => r.GetProperty("kind").GetString() == "EventField"), "fieldName").ShouldBe("shotsInWindow");
        Argument(detect, "op").ShouldBe("BecomesGreaterThanOrEqualTo");
        ArgumentElement(detect, "value").GetDouble().ShouldBe(Kql["shotThreshold"]);
    }

    [Fact]
    public void No_view_fires_on_every_event()
    {
        Entities.Where(e => e.GetProperty("type").GetString() == "timeSeriesView-v1" && Has(e, "instance"))
            .SelectMany(Rows)
            .ShouldNotContain(r => r.GetProperty("kind").GetString() == "OnEveryValue");
    }

    private static string ObjectId() =>
        Views("Object").Single(e => e.GetProperty("payload").GetProperty("name").GetString() == "Pressure spell")
            .GetProperty("uniqueIdentifier").GetString()!;

    private static IEnumerable<JsonElement> Views(string type) =>
        Entities.Where(e => e.GetProperty("type").GetString() == "timeSeriesView-v1"
            && e.GetProperty("payload").GetProperty("definition").GetProperty("type").GetString() == type);

    private static JsonElement Entity(string id) => Entities.Single(e => e.GetProperty("uniqueIdentifier").GetString() == id);

    private static string? Parent(JsonElement entity) =>
        entity.GetProperty("payload").TryGetProperty("parentObject", out var parent) ? parent.GetProperty("targetUniqueIdentifier").GetString() : null;

    private static bool Has(JsonElement entity, string property) =>
        entity.GetProperty("payload").TryGetProperty("definition", out var definition) && definition.TryGetProperty(property, out _);

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
