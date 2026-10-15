using Pitchside.Agents.Checking;
using Pitchside.Agents.Personalisation;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Rendering;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

public sealed class PersonaTests
{
    private const string Casual = """{ "sentences": [ { "text": "Nathan Kim scores in minute [[match.clock.minute]]." } ] }""";
    private const string NoObjections = """{ "verdicts": [] }""";

    private static readonly DetectedMoment Goal = Matches.Moment(7, "MER-7:M:0324:goal");

    private static async Task<MomentContext> BaseAsync(DetectedMoment moment) => await Crew.Workflow().RunAsync(moment.WorkItem, CancellationToken.None);

    private static ChatResult Script(ChatRequest request, string persona) => new(persona, "fake", "fake-model", 0, 0);

    public static TheoryData<string> SevenMoments()
    {
        var data = new TheoryData<string>();
        foreach (var moment in Matches.Moments(7))
        {
            data.Add(moment.Moment.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SevenMoments))]
    public async Task Every_seed_7_moment_gets_its_verified_personas_within_their_limits(string momentId)
    {
        var moment = Matches.Moment(7, momentId);
        var context = await BaseAsync(moment);

        var variants = await Crew.Personas().WriteAsync(context, context.BaseNarrative!, CancellationToken.None);

        // A moment with no players on it has no one to follow, so no player-focus variant.
        var expected = Personas.Viewer.Where(p => p != Personas.PlayerFocus || moment.Moment.PlayerIds.Count > 0);
        variants.Select(v => v.Persona).Distinct().Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
        variants.ShouldAllBe(v => v.Status == NarrativeStatus.Verified && v.Language == Languages.English);
        foreach (var variant in variants)
        {
            AgentPersonaWriter.ShapeObjections(PersonaSpecs.For(variant.Persona), variant.Sentences).ShouldBeEmpty(variant.Id);
            variant.Evaluation.CitationCoverage.ShouldBe(1);
        }
    }

    [Fact]
    public async Task Club_fan_is_written_for_both_clubs_and_player_focus_for_each_involved_player_only()
    {
        var context = await BaseAsync(Goal);

        var variants = await Crew.Personas().WriteAsync(context, context.BaseNarrative!, CancellationToken.None);

        variants.Where(v => v.Persona == Personas.ClubFan).Select(v => v.Subject).ShouldBe([Goal.Document.Home.Club.Id, Goal.Document.Away.Club.Id], ignoreOrder: true);
        variants.Where(v => v.Persona == Personas.PlayerFocus).Select(v => v.Subject).ShouldBe(Goal.Moment.PlayerIds, ignoreOrder: true);
        variants.Select(v => v.Id).ShouldBeUnique();
        variants.Single(v => v.Subject == "INY-07").Id.ShouldBe("MER-7:M:0324:goal:player_focus.INY-07:en");
    }

    [Fact]
    public async Task A_casual_answer_over_eighteen_words_is_retried_with_the_objection()
    {
        const string Long = """{ "sentences": [ { "text": "Nathan Kim scores a goal that everyone in the stadium and at home will be talking about for a very long time after minute [[match.clock.minute]]." } ] }""";
        var provider = new FakeChatProvider((request, _) => request.PromptName switch
        {
            "persona-casual" when request.Attempt == 1 => Long,
            "persona-casual" => Casual,
            _ => NoObjections,
        });
        var context = await BaseAsync(Goal);

        var variant = await OnlyAsync(provider, context, Personas.Casual);

        variant.Status.ShouldBe(NarrativeStatus.Verified);
        variant.Provenance.Attempts.ShouldBe(2);
        provider.Requests.Single(r => r.PromptName == "persona-casual" && r.Attempt == 2).UserMessage.ShouldContain("words");
    }

    [Fact]
    public async Task A_casual_answer_with_two_numbers_is_rejected_after_the_retry()
    {
        const string TwoNumbers = """{ "sentences": [ { "text": "Nathan Kim scores at [[match.clock.minute]] for [[match.score.away]]." } ] }""";
        var provider = new FakeChatProvider((request, _) => request.PromptName == "persona-casual" ? TwoNumbers : NoObjections);
        var context = await BaseAsync(Goal);

        var variant = await OnlyAsync(provider, context, Personas.Casual);

        variant.Status.ShouldBe(NarrativeStatus.Rejected);
        variant.RenderedText.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_club_fan_answer_with_a_forbidden_term_is_rejected()
    {
        const string Abusive = """{ "sentences": [ { "text": "Inyoni Wanderers make the zorblax opponents look slow in minute [[match.clock.minute]]." } ] }""";
        var provider = new FakeChatProvider((request, _) => request.PromptName == "persona-clubfan" ? Abusive : NoObjections);
        var checker = new CodeFactChecker([.. Crew.Catalog.ForbiddenHashes, CodeFactChecker.Hash("zorblax")]);
        var context = await BaseAsync(Goal);

        var variants = await Crew.Personas(provider, checker).WriteAsync(context, context.BaseNarrative!, CancellationToken.None);

        variants.Where(v => v.Persona == Personas.ClubFan).ShouldAllBe(v => v.Status == NarrativeStatus.Rejected);
    }

    [Fact]
    public async Task Player_focus_may_not_name_a_player_outside_the_moment()
    {
        var bystander = Goal.Document.Home.Starters.First(p => !Goal.Moment.PlayerIds.Contains(p.Id));
        var answer = $$"""{ "sentences": [ { "text": "Nathan Kim beats {{bystander.Name}} in minute [[match.clock.minute]]." } ] }""";
        var provider = new FakeChatProvider((request, _) => request.PromptName == "persona-player" ? answer : NoObjections);
        var context = await BaseAsync(Goal);

        var variants = await Crew.Personas(provider).WriteAsync(context, context.BaseNarrative!, CancellationToken.None);

        variants.Where(v => v.Persona == Personas.PlayerFocus).ShouldAllBe(v => v.Status == NarrativeStatus.Rejected);
        var check = Crew.Checker.Check(
            [NarrativeRenderer.Cite($"Nathan Kim beats {bystander.Name} in minute [[match.clock.minute]].")],
            Goal.Sheet, Goal.Moment, Goal.Document, Languages.English, Goal.Moment.PlayerIds.ToHashSet(StringComparer.Ordinal));
        check.Objections.ShouldContain(o => o.Kind == ObjectionKinds.ForbiddenContent && o.Detail.Contains(bystander.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_four_personas_say_four_different_things_from_the_same_facts()
    {
        var context = await BaseAsync(Goal);

        var variants = await Crew.Personas().WriteAsync(context, context.BaseNarrative!, CancellationToken.None);

        var byPersona = variants.GroupBy(v => v.Persona).Select(g => g.First().RenderedText).ToList();
        byPersona.Distinct(StringComparer.Ordinal).Count().ShouldBe(4);
        variants.ShouldAllBe(v => v.FactSheetSeq == context.FactSheet.AsOfSeq);
    }

    private static async Task<Narrative> OnlyAsync(FakeChatProvider provider, MomentContext context, string persona)
    {
        var variants = await Crew.Personas(provider).WriteAsync(context, context.BaseNarrative!, CancellationToken.None);
        return variants.Single(v => v.Persona == persona);
    }
}
