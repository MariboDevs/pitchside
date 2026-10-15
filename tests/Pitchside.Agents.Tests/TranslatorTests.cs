using Pitchside.Agents.Personalisation;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

public sealed class TranslatorTests
{
    private const string NoObjections = """{ "verdicts": [] }""";

    private static readonly DetectedMoment Goal = Matches.Moment(7, "MER-7:M:0324:goal");

    private static async Task<MomentContext> BaseAsync() => await Crew.Workflow().RunAsync(Goal.WorkItem, CancellationToken.None);

    [Fact]
    public async Task Each_english_narrative_is_translated_into_the_three_other_languages_with_its_tokens_intact()
    {
        var context = await BaseAsync();

        var translations = await Crew.Translator().TranslateAsync(context, [context.BaseNarrative!], CancellationToken.None);

        translations.Select(t => t.Language).ShouldBe([Languages.Afrikaans, Languages.IsiZulu, Languages.Spanish], ignoreOrder: true);
        translations.ShouldAllBe(t => t.Status == NarrativeStatus.Verified && !t.Provenance.Fallback);
        foreach (var translation in translations)
        {
            AgentTranslator.Mismatches(context.BaseNarrative!.Sentences, translation.Sentences, ["Nathan Kim", "Inyoni Wanderers"]).ShouldBeEmpty();
            translation.Id.ShouldBe(Narrative.IdFor(Goal.Moment.Id, Personas.Base, translation.Language));
        }
    }

    [Fact]
    public async Task A_dropped_token_falls_back_to_the_english_sentences()
    {
        const string Dropped = """{ "sentences": [ { "text": "Nathan Kim score vir Inyoni Wanderers." } ] }""";
        var provider = new FakeChatProvider((request, _) => request.PromptName == AgentTranslator.PromptName ? Dropped : NoObjections);
        var context = await BaseAsync();

        var translations = await Crew.Translator(provider).TranslateAsync(context, [context.BaseNarrative!], CancellationToken.None);

        translations.ShouldAllBe(t => t.Status == NarrativeStatus.Fallback && t.Provenance.Fallback);
        translations.ShouldAllBe(t => t.Sentences.SequenceEqual(context.BaseNarrative!.Sentences));
    }

    [Fact]
    public async Task A_translated_name_falls_back()
    {
        var english = (await BaseAsync()).BaseNarrative!;
        var renamed = english.Sentences.Select(s => new { text = s.Text.Replace("Nathan Kim", "Natán Kim", StringComparison.Ordinal) });
        var answer = System.Text.Json.JsonSerializer.Serialize(new { sentences = renamed });
        var provider = new FakeChatProvider((request, _) => request.PromptName == AgentTranslator.PromptName ? answer : NoObjections);
        var context = await BaseAsync();

        var translations = await Crew.Translator(provider).TranslateAsync(context, [english], CancellationToken.None);

        translations.ShouldAllBe(t => t.Status == NarrativeStatus.Fallback);
        AgentTranslator.Mismatches(english.Sentences, [.. renamed.Select(r => Rendering.NarrativeRenderer.Cite(r.text))], ["Nathan Kim"])
            .ShouldContain(m => m.Contains("'Nathan'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_short_name_counts_as_a_name_and_repeated_tokens_must_repeat()
    {
        IReadOnlyList<NarrativeSentence> source = [Rendering.NarrativeRenderer.Cite("Kim and the Wanderers have [[home.shots]] then [[home.shots]] again.")];
        string[] names = ["Nathan Kim", "Inyoni Wanderers"];

        AgentTranslator.Mismatches(source, [Rendering.NarrativeRenderer.Cite("Kim en die Wanderers het [[home.shots]] en weer [[home.shots]].")], names).ShouldBeEmpty();
        AgentTranslator.Mismatches(source, [Rendering.NarrativeRenderer.Cite("Kimi en die Wanderers het [[home.shots]] en weer [[home.shots]].")], names)
            .ShouldContain(m => m.Contains("'Kim'", StringComparison.Ordinal));
        AgentTranslator.Mismatches(source, [Rendering.NarrativeRenderer.Cite("Kim en die Swerwers het [[home.shots]] en weer [[home.shots]].")], names)
            .ShouldContain(m => m.Contains("'Wanderers'", StringComparison.Ordinal));
        AgentTranslator.Mismatches(source, [Rendering.NarrativeRenderer.Cite("Kim en die Wanderers het [[home.shots]].")], names)
            .ShouldContain(m => m.StartsWith("The tokens changed", StringComparison.Ordinal));
    }

    [Fact]
    public void Swapped_surnames_are_a_mismatch_even_though_every_word_survives()
    {
        IReadOnlyList<NarrativeSentence> source = [Rendering.NarrativeRenderer.Cite("Nathan Kim and Femi Zielinski combine in minute [[match.clock.minute]].")];
        string[] names = ["Nathan Kim", "Femi Zielinski"];

        AgentTranslator.Mismatches(source, [Rendering.NarrativeRenderer.Cite("Nathan Kim y Femi Zielinski combinan en el minuto [[match.clock.minute]].")], names).ShouldBeEmpty();
        AgentTranslator.Mismatches(source, [Rendering.NarrativeRenderer.Cite("Nathan Zielinski y Femi Kim combinan en el minuto [[match.clock.minute]].")], names)
            .ShouldContain(m => m.Contains("'Nathan Kim'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_full_name_appears_as_often_as_in_the_english_no_more_and_no_less()
    {
        IReadOnlyList<NarrativeSentence> source = [Rendering.NarrativeRenderer.Cite("Nathan Kim scores in minute [[match.clock.minute]].")];
        string[] names = ["Nathan Kim", "Femi Zielinski"];

        AgentTranslator.Mismatches(source, [Rendering.NarrativeRenderer.Cite("Nathan Kim, Nathan Kim marca en el minuto [[match.clock.minute]].")], names)
            .ShouldContain(m => m.Contains("'Nathan Kim'", StringComparison.Ordinal));
        AgentTranslator.Mismatches(source, [Rendering.NarrativeRenderer.Cite("Nathan Kim y Femi Zielinski marcan en el minuto [[match.clock.minute]].")], names)
            .ShouldContain(m => m.Contains("'Femi Zielinski'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_translation_keeps_its_personas_shape_or_falls_back()
    {
        var context = await BaseAsync();
        var casual = NarrativeFactory.Create(context, Personas.Casual, Languages.English, [Rendering.NarrativeRenderer.Cite("Kim scores for Inyoni Wanderers.")], NarrativeStatus.Verified,
            new NarrativeProvenance("fake", "fake", "persona-casual-v1", 1, DateTimeOffset.UnixEpoch, false));
        const string TwoSentences = """{ "sentences": [ { "text": "Kim score vir Inyoni Wanderers." }, { "text": "Wat 'n doel." } ] }""";
        var provider = new FakeChatProvider((request, _) => request.PromptName == AgentTranslator.PromptName ? TwoSentences : NoObjections);

        var translations = await Crew.Translator(provider).TranslateAsync(context, [casual], CancellationToken.None);

        translations.ShouldAllBe(t => t.Status == NarrativeStatus.Fallback && t.Provenance.Fallback);
    }

    [Fact]
    public async Task A_rejected_variant_reaches_every_language_as_rejected_without_a_model_call()
    {
        var context = await BaseAsync();
        var rejected = NarrativeFactory.Create(context, Personas.Analyst, Languages.English, [Rendering.NarrativeRenderer.Cite("Too long.")], NarrativeStatus.Rejected,
            new NarrativeProvenance("fake", "fake", "persona-analyst-v1", 2, DateTimeOffset.UnixEpoch, false));
        var provider = new FakeChatProvider((_, _) => throw new InvalidOperationException("No call expected."));

        var translations = await Crew.Translator(provider).TranslateAsync(context, [rejected], CancellationToken.None);

        translations.Select(t => t.Language).ShouldBe([Languages.Afrikaans, Languages.IsiZulu, Languages.Spanish], ignoreOrder: true);
        translations.ShouldAllBe(t => t.Status == NarrativeStatus.Rejected && t.Persona == Personas.Analyst);
        provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failing_provider_falls_back_rather_than_losing_the_language()
    {
        var provider = new FakeChatProvider((request, _) => request.PromptName == AgentTranslator.PromptName
            ? throw new HttpRequestException("The deployment is unavailable.")
            : NoObjections);
        var context = await BaseAsync();

        var translations = await Crew.Translator(provider).TranslateAsync(context, [context.BaseNarrative!], CancellationToken.None);

        translations.Count.ShouldBe(3);
        translations.ShouldAllBe(t => t.Status == NarrativeStatus.Fallback);
    }

    [Fact]
    public async Task The_crew_publishes_each_narrative_once_as_it_completes_the_base_first()
    {
        var published = new System.Collections.Concurrent.ConcurrentQueue<Narrative>();

        var context = await Crew.FullWorkflow().RunAsync(Goal.WorkItem, (n, _) =>
        {
            published.Enqueue(n);
            return Task.CompletedTask;
        }, CancellationToken.None);

        published.First().Id.ShouldBe(context.BaseNarrative!.Id);
        published.Select(n => n.Id).Order(StringComparer.Ordinal).ShouldBe(context.Narratives.Select(n => n.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_variants_support_check_sees_both_sides_and_every_cited_fact()
    {
        var context = await BaseAsync();
        var cited = context.FactSheet.Facts.First(f => f.Key.StartsWith("player.", StringComparison.Ordinal) && !Goal.Moment.PlayerIds.Any(id => f.Key.Contains(id, StringComparison.Ordinal)));

        var input = Prompts.ExplainerInput.BuildVariantCheck(context, [Rendering.NarrativeRenderer.Cite($"Compare [[{cited.Key}]] with [[home.shots]] and [[away.shots]].")]);

        input.ShouldContain("\"home.shots\"");
        input.ShouldContain("\"away.shots\"");
        input.ShouldContain($"\"{cited.Key}\"");
    }

    [Fact]
    public async Task The_full_crew_writes_every_persona_in_every_language()
    {
        var context = await Crew.FullWorkflow().RunAsync(Goal.WorkItem, CancellationToken.None);

        // base, analyst, casual, a club fan per club and a player focus per involved player, in four languages.
        var english = 1 + 1 + 1 + 2 + Goal.Moment.PlayerIds.Count;
        context.Narratives.Count.ShouldBe(english * Languages.All.Count);
        context.Narratives.Select(n => n.Id).ShouldBeUnique();
        context.Narratives.ShouldAllBe(n => n.Status == NarrativeStatus.Verified);
    }
}
