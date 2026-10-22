using System.Text.Json;
using System.Text.RegularExpressions;
using Pitchside.Contracts;

namespace Pitchside.Fabric.Tests;

/// <summary>
/// The KQL database script: every file is a valid schema script, the landing table matches the envelope the bus
/// carries, and the typed table's columns come from schemas/event.schema.json.
/// </summary>
public sealed partial class KqlScriptTests
{
    private const string Tables = "fabric/kql/schema/01-tables.kql";
    private const string UpdatePolicy = "fabric/kql/schema/02-update-policy.kql";

    /// <summary>The columns WP-12 names, in its order.</summary>
    private static readonly string[] SpecColumns =
        ["matchId", "seq", "period", "clockMs", "type", "teamId", "playerId", "x", "y", "xg", "outcome", "difficulty", "lengthM", "speedKph", "publishedUtc"];

    public static TheoryData<string> Files() => [.. KqlFile.All()];

    [Theory]
    [MemberData(nameof(Files))]
    public void Every_command_is_a_management_command_with_no_empty_line_inside(string file)
    {
        var commands = KqlFile.Commands(file);

        commands.ShouldNotBeEmpty();
        foreach (var command in commands)
        {
            command.ShouldStartWith(".", customMessage: $"{file}: an empty line inside a command splits it; this block does not start a command:\n{command}");
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Every_command_is_one_a_Fabric_database_definition_accepts(string file)
    {
        // Fabric refuses the whole DatabaseSchema.kql (ScriptContainsUnsupportedCommand) when any command starts
        // otherwise; the list is the one its error names.
        foreach (var command in KqlFile.Commands(file))
        {
            DefinitionCommand().IsMatch(command).ShouldBeTrue($"{file}: Fabric does not accept this command in a database definition:\n{command}");
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Top_sorts_by_a_single_expression(string file)
    {
        // KQL's top takes one sort expression; a tie-breaker needs order by and take.
        foreach (Match top in MultiKeyTop().Matches(KqlFile.Read(file)))
        {
            top.Success.ShouldBeFalse($"{file}: {top.Value.Trim()}");
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Each_saved_query_file_creates_exactly_one_function(string file)
    {
        if (file.StartsWith("fabric/kql/schema/", StringComparison.Ordinal))
        {
            return;
        }

        KqlFile.Functions(file).Count.ShouldBe(KqlFile.Commands(file).Count, file);
        KqlFile.Functions(file).Count.ShouldBe(1, file);
    }

    [Fact]
    public void Function_names_are_unique_across_the_script()
    {
        var functions = KqlFile.All().SelectMany(KqlFile.Functions).ToList();

        functions.ShouldBeUnique();
        functions.ShouldContain("ExpandEnvelope");
    }

    [Fact]
    public void The_events_table_starts_with_the_columns_the_spec_names()
    {
        var columns = Columns("events");

        columns.Take(SpecColumns.Length).Select(c => c.Name).ShouldBe(SpecColumns);
        columns.ToDictionary(c => c.Name, c => c.Type).ShouldContainKeyAndValue("seq", "long");
        columns.ToDictionary(c => c.Name, c => c.Type).ShouldContainKeyAndValue("publishedUtc", "datetime");
    }

    [Fact]
    public void The_update_policy_projects_exactly_the_events_columns_in_order()
    {
        var projected = ProjectedColumn().Matches(KqlFile.Read(UpdatePolicy)).Select(m => m.Groups["name"].Value).ToList();

        projected.ShouldBe(Columns("events").Select(c => c.Name));
    }

    [Fact]
    public void The_update_policy_reads_only_fields_the_event_schema_defines()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.Combine("schemas", "event.schema.json")));
        var properties = schema.RootElement.GetProperty("properties");
        var location = schema.RootElement.GetProperty("$defs").GetProperty("location").GetProperty("properties");

        var paths = PayloadPath().Matches(KqlFile.Read(UpdatePolicy)).Select(m => m.Groups["path"].Value).Distinct().ToList();

        paths.ShouldNotBeEmpty();
        foreach (var path in paths)
        {
            var parts = path.Split('.');
            properties.TryGetProperty(parts[0], out _).ShouldBeTrue($"e.{path}: {parts[0]} is not in schemas/event.schema.json");
            if (parts.Length == 2)
            {
                location.TryGetProperty(parts[1], out _).ShouldBeTrue($"e.{path}: {parts[1]} is not a location field");
            }
        }
    }

    [Fact]
    public void The_landing_table_and_its_mapping_match_the_envelope_the_bus_publishes()
    {
        var envelope = new EventEnvelope("MER-7", 1, "kick_off", "{}", DateTimeOffset.UnixEpoch);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(envelope, ContractJson.Options));
        var fields = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Columns("events_raw").Select(c => c.Name).ShouldBe(fields);
        MappingPath().Matches(KqlFile.Read(Tables)).Select(m => m.Groups["path"].Value).ShouldBe(fields);
    }

    private static List<(string Name, string Type)> Columns(string table)
    {
        var definition = Regex.Match(KqlFile.Read(Tables), $@"^\.create-merge table {table} \((?<columns>[^)]*)\)", RegexOptions.Multiline);
        definition.Success.ShouldBeTrue($"no .create-merge table {table}");
        return
        [
            .. definition.Groups["columns"].Value
                .Split(',')
                .Select(column => column.Split(':', StringSplitOptions.TrimEntries))
                .Select(parts => (parts[0], parts[1])),
        ];
    }

    [GeneratedRegex(@"^\s{8}(?<name>\w+)(?:\s*=|,|$)", RegexOptions.Multiline)]
    private static partial Regex ProjectedColumn();

    [GeneratedRegex(@"^\.(create-merge table|create-or-alter function|create-or-alter materialized-view|alter materialized-view|alter table \S+ policy (update|retention|caching|streamingingestion|ingestionbatching|encoding)|alter column \S+ policy encoding type|create-or-alter table \S+ ingestion \w+ mapping|create table \S+ ingestion \w+ mapping)\b")]
    private static partial Regex DefinitionCommand();

    [GeneratedRegex(@"\|\s*top\s+\d+\s+by\s+[^|\n]*,")]
    private static partial Regex MultiKeyTop();

    [GeneratedRegex(@"\be\.(?<path>\w+(?:\.\w+)?)")]
    private static partial Regex PayloadPath();

    [GeneratedRegex(@"""Path"":""\$\.(?<path>\w+)""")]
    private static partial Regex MappingPath();
}
