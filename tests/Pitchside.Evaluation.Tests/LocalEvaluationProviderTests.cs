using Pitchside.Contracts;

namespace Pitchside.Evaluation.Tests;

public sealed class LocalEvaluationProviderTests
{
    [Fact]
    public async Task The_proxy_is_the_share_of_sentences_citing_only_the_moments_evidence()
    {
        var narrative = Items.With(
            new NarrativeSentence("Amanzi lead [[match.score.home]] with [[home.xg]] xG.", ["match.score.home", "home.xg"]),
            new NarrativeSentence("Inyoni have [[away.shots]] shots.", ["away.shots"]),
            new NarrativeSentence("What a finish.", []));

        var score = await new LocalEvaluationProvider().ScoreAsync(new EvaluationItem(narrative, Items.Moment, Items.Sheet), CancellationToken.None);

        score.ShouldBe(2.0 / 3);
    }

    [Fact]
    public void A_narrative_without_sentences_has_no_score() =>
        LocalEvaluationProvider.Score(Items.With(), Items.Moment).ShouldBeNull();
}
