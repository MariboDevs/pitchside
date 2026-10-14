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
            .ShouldContain(m => m.Contains("Nathan Kim", StringComparison.Ordinal));
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
