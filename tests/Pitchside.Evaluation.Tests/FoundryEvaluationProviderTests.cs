using Microsoft.Extensions.AI;
using Pitchside.Contracts;
using Pitchside.Runtime.Audit;

namespace Pitchside.Evaluation.Tests;

public sealed class FoundryEvaluationProviderTests
{
    [Fact]
    public async Task The_judges_score_is_normalised_and_the_call_is_audited()
    {
        var judge = new JudgeChatClient("<S0>checked each figure</S0><S1>all supported</S1><S2>4</S2>");
        var audit = new InMemoryAuditStore();
        var provider = new FoundryEvaluationProvider(judge, audit, TimeProvider.System);
        var narrative = Items.With(new NarrativeSentence("Amanzi lead [[match.score.home]] with [[home.xg]] xG.", ["match.score.home", "home.xg"]));

        var score = await provider.ScoreAsync(new EvaluationItem(narrative, Items.Moment, Items.Sheet), CancellationToken.None);

        score.ShouldBe(0.75);
        var prompt = string.Join('\n', judge.Seen.Select(m => m.Text));
        prompt.ShouldContain("home.xg = 1.24 xg");
        prompt.ShouldContain("match.score.home = 1 goals");
        prompt.ShouldContain("Amanzi lead 1-0 with 1.24 xG.");
        prompt.ShouldNotContain("away.shots");
        var record = (await audit.ListAsync("MER-7", null, 10, CancellationToken.None)).ShouldHaveSingleItem();
        (record.Stage, record.Provider, record.Outcome, record.MomentId).ShouldBe(("groundedness", "foundry", AuditOutcome.Ok, Items.Moment.Id));

        // The hash is of the system prompt the evaluator actually sent, and its text is never stored.
        var system = string.Join('\n', judge.Seen.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        system.ShouldNotBeEmpty();
        record.PromptHash.ShouldBe(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(system))));
    }

    [Fact]
    public async Task An_audit_store_that_fails_never_loses_the_score()
    {
        var provider = new FoundryEvaluationProvider(new JudgeChatClient("<S0>checked</S0><S1>supported</S1><S2>5</S2>"), new FailingAuditStore(), TimeProvider.System);

        (await provider.ScoreAsync(new EvaluationItem(Items.With(), Items.Moment, Items.Sheet), CancellationToken.None)).ShouldBe(1.0);
    }

    [Fact]
    public async Task An_audit_store_that_fails_never_hides_the_judges_failure()
    {
        var provider = new FoundryEvaluationProvider(new JudgeChatClient(null), new FailingAuditStore(), TimeProvider.System);

        await Should.ThrowAsync<InvalidOperationException>(() => provider.ScoreAsync(new EvaluationItem(Items.With(), Items.Moment, Items.Sheet), CancellationToken.None));
    }

    [Fact]
    public async Task A_failed_judgement_is_audited_and_still_fails()
    {
        var audit = new InMemoryAuditStore();
        var provider = new FoundryEvaluationProvider(new JudgeChatClient(null), audit, TimeProvider.System);

        await Should.ThrowAsync<InvalidOperationException>(() => provider.ScoreAsync(new EvaluationItem(Items.With(), Items.Moment, Items.Sheet), CancellationToken.None));

        (await audit.ListAsync("MER-7", null, 10, CancellationToken.None)).ShouldHaveSingleItem().Outcome.ShouldBe(AuditOutcome.Error);
    }

    [Fact]
    public void The_context_names_the_clubs_and_the_players_behind_the_facts()
    {
        var document = new MatchDocument("0.2", "MER-7", 7, "Meridian League", "Amanzi United Stadium", DateTimeOffset.UnixEpoch,
            new TeamSheet(new Club("AMZ", "Amanzi United", "Amanzi", "#0A3D62", "#FFFFFF"), "4-3-3", [new Player("AMZ-02", "Yaw Greco", 2, Position.Defender, 110)], []),
            new TeamSheet(new Club("INY", "Inyoni Wanderers", "Inyoni", "#6D214F", "#FFFFFF"), "4-4-2", [new Player("INY-07", "Nathan Kim", 7, Position.Midfielder, 105)], []));
        var narrative = Items.With() with { RenderedText = "Nathan Kim watches Amanzi lead 1-0." };

        var context = FoundryEvaluationProvider.Context(new EvaluationItem(narrative, Items.Moment, Items.Sheet, document));

        context.ShouldContain("home = Amanzi United (AMZ)");
        context.ShouldContain("away = Inyoni Wanderers (INY)");
        context.ShouldContain("player INY-07 = Nathan Kim, number 7, Inyoni Wanderers");
        context.ShouldNotContain("Yaw Greco");
    }

    [Theory]
    [InlineData(1.0, 0.0)]
    [InlineData(3.0, 0.5)]
    [InlineData(5.0, 1.0)]
    [InlineData(7.0, 1.0)]
    public void Scores_from_one_to_five_become_zero_to_one(double score, double expected) =>
        FoundryEvaluationProvider.Normalise(score).ShouldBe(expected);

    [Fact]
    public void No_score_stays_no_score() => FoundryEvaluationProvider.Normalise(null).ShouldBeNull();
}
