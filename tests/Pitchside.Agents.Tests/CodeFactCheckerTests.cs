using System.Text.Json;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Rendering;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

public sealed class CodeFactCheckerTests
{
    private static readonly DetectedMoment Goal = Matches.Moment(7, "MER-7:M:0324:goal");

    private static Verdict Check(params string[] sentences) =>
        Crew.Checker.Check([.. sentences.Select(NarrativeRenderer.Cite)], Goal.Sheet, Goal.Moment, Goal.Document);

    public static TheoryData<string, string[]> Corpus()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.Combine("tests", "Pitchside.Agents.Tests", "fixtures", "checker-corpus.json")));
        var data = new TheoryData<string, string[]>();
        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            data.Add(item.GetProperty("text").GetString()!, [.. item.GetProperty("expected").EnumerateArray().Select(e => e.GetString()!)]);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void The_corpus_gets_its_expected_verdict(string text, string[] expected)
    {
        var verdict = Check(text);
        verdict.Objections.Select(o => o.Kind).Distinct().Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal), text);
        verdict.Passed.ShouldBe(expected.Length == 0);
    }

    [Fact]
    public void The_corpus_has_at_least_thirty_sentences() => Corpus().Count.ShouldBeGreaterThanOrEqualTo(30);

    [Fact]
    public void An_unknown_key_is_unknown_fact() =>
        Check("Inyoni Wanderers have [[away.expectedThreat]].").Objections.ShouldHaveSingleItem().Kind.ShouldBe(ObjectionKinds.UnknownFact);

    [Fact]
    public void A_digit_outside_a_token_is_uncited_number()
    {
        var objection = Check("Inyoni Wanderers lead 1-0.").Objections.ShouldHaveSingleItem();
        objection.Kind.ShouldBe(ObjectionKinds.UncitedNumber);
        objection.SentenceIndex.ShouldBe("0");
        objection.Detail.ShouldContain("1-0");
    }

    [Fact]
    public void A_shirt_number_and_halves_in_words_are_allowed() =>
        Check("Nathan Kim, number #7, scores in the first half.").Passed.ShouldBeTrue();

    [Fact]
    public void A_name_not_in_the_document_is_forbidden_content() =>
        Check("Nathan Kim beats Diego Ramos.").Objections.Select(o => o.Kind).Distinct().ShouldBe([ObjectionKinds.ForbiddenContent]);

    [Fact]
    public void A_player_not_on_the_moment_is_wrong_scope() =>
        Check("Hugo Sato has [[player.INY-01.passes]] passes.").Objections.ShouldHaveSingleItem().Kind.ShouldBe(ObjectionKinds.WrongScope);

    [Theory]
    [InlineData("odds")]
    [InlineData("bet")]
    [InlineData("bookmaker")]
    [InlineData("wager")]
    [InlineData("Withheld03")]
    public void Forbidden_vocabulary_is_forbidden_content(string word) =>
        Check($"Inyoni Wanderers and the {word.ToLowerInvariant()} story.").Objections.ShouldContain(o => o.Kind == ObjectionKinds.ForbiddenContent);

    [Fact]
    public void Forbidden_words_match_whole_words_only() =>
        Check("Between the better sides, Inyoni Wanderers lead.").Passed.ShouldBeTrue();

    [Fact]
    public void An_empty_draft_fails() => Check().Passed.ShouldBeFalse();

    [Fact]
    public void Every_sentence_is_indexed()
    {
        var verdict = Check("Nathan Kim scores.", "Inyoni Wanderers lead 1-0.");
        verdict.Objections.ShouldHaveSingleItem().SentenceIndex.ShouldBe("1");
    }
}
