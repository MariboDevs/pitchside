using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pitchside.Fabric.Tests;

/// <summary>
/// The Real-Time Dashboard and the queryset definitions load in Fabric and call the committed KQL. Fabric checks
/// these rules only when the definition is uploaded, so they are checked here first: every id is an RFC 4122 UUID and
/// unique in its kind, every query is referenced exactly once, references resolve, and every variable a query uses
/// is a parameter.
/// </summary>
public sealed partial class DashboardDefinitionTests
{
    private static readonly string[] SavedQueries = ["ShotsBySide", "XgRace", "PassCompletionByPlayer", "PressingIntensity", "MatchMomentum", "TopSpeeds"];

    private static readonly HashSet<string> Functions = [.. KqlFile.All().SelectMany(KqlFile.Functions)];

    private static JsonElement Dashboard { get; } = Load("fabric/dashboard/pitchside-live.json");

    private static JsonElement Queryset { get; } = Load("fabric/queryset/match-analysis.json").GetProperty("queryset");

    [Theory]
    [InlineData("pages")]
    [InlineData("dataSources")]
    [InlineData("parameters")]
    [InlineData("queries")]
    [InlineData("tiles")]
    public void Ids_are_unique_uuids(string kind)
    {
        var ids = Dashboard.GetProperty(kind).EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToList();

        ids.ShouldNotBeEmpty();
        ids.ShouldBeUnique();
        foreach (var id in ids)
        {
            Guid.TryParseExact(id, "D", out _).ShouldBeTrue($"{kind}: {id} is not an RFC 4122 UUID");
        }
    }

    [Fact]
    public void Every_query_is_referenced_exactly_once()
    {
        var references = Dashboard.GetProperty("tiles").EnumerateArray().Select(t => t.GetProperty("queryRef"))
            .Concat(Dashboard.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("dataSource").GetProperty("queryRef")))
            .Concat(Dashboard.GetProperty("baseQueries").EnumerateArray())
            .Select(r => r.GetProperty("queryId").GetString())
            .ToList();
        var queries = Dashboard.GetProperty("queries").EnumerateArray().Select(q => q.GetProperty("id").GetString()).ToList();

        references.ShouldBeUnique();
        references.Order(StringComparer.Ordinal).ShouldBe(queries.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Tiles_sit_on_a_page_and_queries_on_a_data_source()
    {
        var pages = Dashboard.GetProperty("pages").EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToHashSet();
        var sources = Dashboard.GetProperty("dataSources").EnumerateArray().Select(d => d.GetProperty("id").GetString()).ToHashSet();

        Dashboard.GetProperty("tiles").EnumerateArray().ShouldAllBe(t => pages.Contains(t.GetProperty("pageId").GetString()));
        Dashboard.GetProperty("queries").EnumerateArray().ShouldAllBe(q => sources.Contains(q.GetProperty("dataSource").GetProperty("dataSourceId").GetString()));
    }

    [Fact]
    public void Every_variable_a_query_uses_is_a_parameter_and_is_declared()
    {
        var variables = Dashboard.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("variableName").GetString()!).ToHashSet();

        foreach (var query in Dashboard.GetProperty("queries").EnumerateArray())
        {
            var text = query.GetProperty("text").GetString()!;
            var used = query.GetProperty("usedVariables").EnumerateArray().Select(v => v.GetString()!).ToHashSet();
            used.ShouldBeSubsetOf(variables);
            Variable().Matches(text).Select(m => m.Value).ToHashSet().ShouldBe(used, ignoreOrder: true, customMessage: text);
        }
    }

    [Fact]
    public void The_six_tiles_show_the_six_saved_queries()
    {
        var tiles = Dashboard.GetProperty("tiles").EnumerateArray().ToList();
        var queries = Dashboard.GetProperty("queries").EnumerateArray().ToDictionary(q => q.GetProperty("id").GetString()!, q => q.GetProperty("text").GetString()!);

        tiles.Count.ShouldBe(6);
        tiles.Select(t => Called(queries[t.GetProperty("queryRef").GetProperty("queryId").GetString()!]).Single()).ShouldBe(SavedQueries, ignoreOrder: true);
    }

    [Fact]
    public void The_match_selector_lists_the_matches_in_the_table()
    {
        var parameter = Dashboard.GetProperty("parameters").EnumerateArray().Single();
        var queryId = parameter.GetProperty("dataSource").GetProperty("queryRef").GetProperty("queryId").GetString();
        var text = Dashboard.GetProperty("queries").EnumerateArray().Single(q => q.GetProperty("id").GetString() == queryId).GetProperty("text").GetString()!;

        parameter.GetProperty("variableName").GetString().ShouldBe("_matchId");
        parameter.GetProperty("selectionType").GetString().ShouldBe("single");
        Called(text).ShouldBe(["Matches"]);
    }

    [Fact]
    public void Ids_of_the_environment_are_placeholders_the_apply_script_fills()
    {
        var source = Dashboard.GetProperty("dataSources").EnumerateArray().Single();

        source.GetProperty("workspace").GetString().ShouldBe("{{WORKSPACE_ID}}");
        source.GetProperty("database").GetString().ShouldBe("{{DATABASE_ID}}");
        source.GetProperty("clusterUri").GetString().ShouldBe("{{QUERY_SERVICE_URI}}");
        Queryset.GetProperty("dataSources")[0].GetProperty("clusterUri").GetString().ShouldBe("{{QUERY_SERVICE_URI}}");
    }

    [Fact]
    public void Every_function_the_dashboard_and_queryset_call_exists()
    {
        var texts = Dashboard.GetProperty("queries").EnumerateArray().Select(q => q.GetProperty("text").GetString()!)
            .Concat(Queryset.GetProperty("tabs").EnumerateArray().Select(t => t.GetProperty("content").GetString()!));

        foreach (var text in texts)
        {
            Called(text).ShouldAllBe(name => Functions.Contains(name), text);
        }
    }

    [Fact]
    public void The_queryset_has_a_tab_per_saved_query_and_the_evidence_tabs()
    {
        var called = Queryset.GetProperty("tabs").EnumerateArray().SelectMany(t => Called(t.GetProperty("content").GetString()!)).ToHashSet();
        var sources = Queryset.GetProperty("dataSources").EnumerateArray().Select(d => d.GetProperty("id").GetString()).ToHashSet();

        foreach (var name in SavedQueries.Append("MatchEventCount").Append("PressureAlerts"))
        {
            called.ShouldContain(name);
        }

        Queryset.GetProperty("tabs").EnumerateArray().ShouldAllBe(t => sources.Contains(t.GetProperty("dataSourceId").GetString()));
    }

    private static IEnumerable<string> Called(string text) =>
        Call().Matches(text).Select(m => m.Groups["name"].Value).Where(name => char.IsUpper(name[0])).Distinct();

    private static JsonElement Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.Combine(path.Split('/'))));
        return document.RootElement.Clone();
    }

    [GeneratedRegex(@"\b_[a-zA-Z]\w*")]
    private static partial Regex Variable();

    [GeneratedRegex(@"\b(?<name>[A-Za-z]\w*)\(")]
    private static partial Regex Call();
}
