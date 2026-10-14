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
        var objection = Check("Inyoni Wanderers lead 1-0 at minute [[match.clock.minute]].").Objections.ShouldHaveSingleItem();
        objection.Kind.ShouldBe(ObjectionKinds.UncitedNumber);
        objection.SentenceIndex.ShouldBe("0");
        objection.Detail.ShouldContain("1-0");
    }

    [Fact]
    public void A_shirt_number_and_halves_in_words_are_allowed() =>
        Check("Nathan Kim, number #7, scores in the first half at minute [[match.clock.minute]].").Passed.ShouldBeTrue();

    [Theory]
    [InlineData("The score is #99 at minute [[match.clock.minute]].")]
    [InlineData("Femi Zielinski, number #7, plays it through at minute [[match.clock.minute]].")]
    public void A_shirt_number_no_named_player_wears_is_uncited_number(string text) =>
        Check(text).Objections.ShouldHaveSingleItem().Kind.ShouldBe(ObjectionKinds.UncitedNumber);

    [Fact]
    public void A_sentence_without_a_token_is_uncited_sentence()
    {
        var objection = Check("Nathan Kim scores at minute [[match.clock.minute]].", "Nathan Kim celebrates.").Objections.ShouldHaveSingleItem();
        objection.Kind.ShouldBe(ObjectionKinds.UncitedSentence);
        objection.SentenceIndex.ShouldBe("1");
    }

    [Fact]
    public void A_name_not_in_the_document_is_forbidden_content() =>
        Check("Nathan Kim beats Diego Ramos at minute [[match.clock.minute]].").Objections.ShouldHaveSingleItem().Detail.ShouldContain("Diego Ramos");

    [Theory]
    [InlineData("Nathan Zielinski")]
    [InlineData("Kim Nathan")]
    [InlineData("Inyoni United")]
    public void Words_from_different_names_do_not_make_a_name(string invented) =>
        Check($"{invented} scores at minute [[match.clock.minute]].").Objections.ShouldHaveSingleItem().Kind.ShouldBe(ObjectionKinds.ForbiddenContent);

    [Theory]
    [InlineData("Kim scores at minute [[match.clock.minute]].")]
    [InlineData("Nathan Kim and Femi Zielinski combine at minute [[match.clock.minute]].")]
    [InlineData("Kim, Zielinski and the Wanderers press at minute [[match.clock.minute]].")]
    public void Part_of_one_name_is_a_name(string text) => Check(text).Passed.ShouldBeTrue();

    [Fact]
    public void A_player_not_on_the_moment_is_wrong_scope() =>
        Check("Hugo Sato has [[player.INY-01.passes]] passes.").Objections.ShouldHaveSingleItem().Kind.ShouldBe(ObjectionKinds.WrongScope);

    [Theory]
    [InlineData("zorblax")]
    [InlineData("Zorblax")]
    [InlineData("ZORBLAX")]
    public void A_word_on_the_hashed_list_is_forbidden_content(string word)
    {
        var checker = new CodeFactChecker([CodeFactChecker.Hash("zorblax")]);
        var verdict = checker.Check([NarrativeRenderer.Cite($"Inyoni Wanderers and the {word} story at minute [[match.clock.minute]].")], Goal.Sheet, Goal.Moment, Goal.Document);
        verdict.Objections.ShouldContain(o => o.Kind == ObjectionKinds.ForbiddenContent && o.Detail.Contains(word, StringComparison.Ordinal));
    }

    [Fact]
    public void Forbidden_words_match_whole_words_only()
    {
        var checker = new CodeFactChecker([CodeFactChecker.Hash("zorb")]);
        checker.Check([NarrativeRenderer.Cite("The zorblax rises at minute [[match.clock.minute]].")], Goal.Sheet, Goal.Moment, Goal.Document).Passed.ShouldBeTrue();
    }

    [Fact]
    public void Hashes_are_lower_case_sha256_of_the_lower_cased_word() =>
        CodeFactChecker.Hash("ABC").ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");

    [Fact]
    public void An_empty_draft_fails() => Check().Passed.ShouldBeFalse();

    [Fact]
    public void Every_sentence_is_indexed()
    {
        var verdict = Check("Nathan Kim scores at minute [[match.clock.minute]].", "Inyoni Wanderers lead 1-0 at minute [[match.clock.minute]].");
        verdict.Objections.ShouldHaveSingleItem().SentenceIndex.ShouldBe("1");
    }
}
