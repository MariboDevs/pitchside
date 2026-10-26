using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pitchside.Engine.Tests;

/// <summary>A seed replayed again under a loop id (WP-11 demo loop) is the same match with its ids changed.</summary>
public sealed class MatchLoopsTests
{
    [Theory]
    [InlineData(26, 1, "MER-26")]
    [InlineData(26, 2, "MER-26-2")]
    [InlineData(7, 13, "MER-7-13")]
    public void A_loop_names_its_match(long seed, int loop, string matchId) => MatchLoops.MatchIdFor(seed, loop).ShouldBe(matchId);

    [Theory]
    [InlineData(26, "MER-26-2", "MER-26")]
    [InlineData(26, "MER-26-393", "MER-26-392")]
    [InlineData(26, "MER-26", null)]
    [InlineData(26, "MER-26-1", null)]
    [InlineData(26, "MER-26-02", null)]
    [InlineData(26, "MER-26-x", null)]
    [InlineData(26, "MER-261-2", null)]
    [InlineData(7, "MER-26-2", null)]
    public void A_later_loop_knows_the_loop_before_it(long seed, string matchId, string? previous) =>
        MatchLoops.PreviousOf(seed, matchId).ShouldBe(previous);

    [Fact]
    public void There_is_no_loop_before_the_first() => Should.Throw<ArgumentOutOfRangeException>(() => MatchLoops.MatchIdFor(26, 0));

    [Fact]
    public void A_loop_changes_the_ids_and_nothing_else()
    {
        var original = MatchSimulator.Simulate(7);

        var loop = MatchLoops.As(original, "MER-7-2");

        loop.Document.ShouldBe(original.Document with { MatchId = "MER-7-2" });
        loop.Events.Count.ShouldBe(original.Events.Count);
        for (var i = 0; i < loop.Events.Count; i++)
        {
            loop.Events[i].Id.ShouldBe($"MER-7-2-{original.Events[i].Seq:00000}");
            loop.Events[i].ShouldBe(original.Events[i] with { MatchId = "MER-7-2", Id = loop.Events[i].Id });
        }
    }

    [Fact]
    public void The_first_loop_is_the_seed_itself()
    {
        var original = MatchSimulator.Simulate(7);

        MatchLoops.As(original, MatchLoops.MatchIdFor(7, MatchLoops.First)).ShouldBeSameAs(original);
    }

    public static TheoryData<string> SchemasWithMatchIds() =>
    [
        .. Directory.GetFiles(RepositoryRoot.Combine("schemas"), "*.schema.json")
            .Where(path => MatchIdPattern(path) is not null)
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal),
    ];

    [Theory]
    [MemberData(nameof(SchemasWithMatchIds))]
    public void Every_contract_accepts_a_loop_id_and_still_refuses_a_malformed_one(string schema)
    {
        var pattern = new Regex(MatchIdPattern(RepositoryRoot.Combine("schemas", schema))!, RegexOptions.CultureInvariant);

        foreach (var valid in new[] { "MER-26", "MER-26-2", "MER-7-13" })
        {
            pattern.IsMatch(valid).ShouldBeTrue($"{schema} refuses {valid}");
        }

        foreach (var invalid in new[] { "MER-26-", "MER-26-2-3", "MER-x", "mer-26", "MER-26-a" })
        {
            pattern.IsMatch(invalid).ShouldBeFalse($"{schema} accepts {invalid}");
        }
    }

    private static string? MatchIdPattern(string path)
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(path));
        return schema.RootElement.TryGetProperty("properties", out var properties)
            && properties.TryGetProperty("matchId", out var matchId)
            && matchId.TryGetProperty("pattern", out var pattern)
            ? pattern.GetString()
            : null;
    }
}
