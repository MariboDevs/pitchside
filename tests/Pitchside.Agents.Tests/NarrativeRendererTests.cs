using Pitchside.Agents.Rendering;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

public sealed class NarrativeRendererTests
{
    private static readonly FactSheet Sheet = new("MER-1", 10, 1, 60_000,
    [
        new Fact { Key = "match.score.home", Value = FactValue.Of(2), Unit = "goals", Precision = 0, DerivedFrom = [] },
        new Fact { Key = "home.xg", Value = FactValue.Of(1.5), Unit = "xg", Precision = 2, DerivedFrom = [] },
        new Fact { Key = "player.AMZ-09.topSpeedKph", Value = FactValue.Of(32.4), Unit = "kph", Precision = 1, DerivedFrom = [] },
        new Fact { Key = "match.state", Value = FactValue.Of("first_half"), Precision = 0, DerivedFrom = [] },
    ]);

    [Fact]
    public void Tokens_render_at_the_fact_precision()
    {
        NarrativeRenderer.Render(
            [NarrativeRenderer.Cite("Amanzi have [[match.score.home]] goals from [[home.xg]] xG."), NarrativeRenderer.Cite("Top speed [[player.AMZ-09.topSpeedKph]] kph.")],
            Sheet).ShouldBe("Amanzi have 2 goals from 1.50 xG. Top speed 32.4 kph.");
    }

    [Fact]
    public void Text_facts_render_as_text() =>
        NarrativeRenderer.RenderSentence("State: [[match.state]].", Sheet).ShouldBe("State: first_half.");

    [Fact]
    public void An_unknown_token_cannot_render() =>
        Should.Throw<InvalidOperationException>(() => NarrativeRenderer.RenderSentence("[[away.xg]]", Sheet));

    [Fact]
    public void Citations_come_from_the_tokens_in_order_without_repeats() =>
        NarrativeRenderer.Cite("[[home.xg]] then [[match.score.home]] then [[home.xg]]").Citations.ShouldBe(["home.xg", "match.score.home"]);

    [Fact]
    public void Coverage_is_the_share_of_written_tokens_that_resolve()
    {
        NarrativeRenderer.CitationCoverage([NarrativeRenderer.Cite("[[home.xg]] and [[home.xg]]"), NarrativeRenderer.Cite("[[away.xg]]")], Sheet).ShouldBe(2.0 / 3);
        NarrativeRenderer.CitationCoverage([NarrativeRenderer.Cite("[[home.xg]]"), NarrativeRenderer.Cite("No number here.")], Sheet).ShouldBe(1);
        NarrativeRenderer.CitationCoverage([NarrativeRenderer.Cite("No number here.")], Sheet).ShouldBe(1);
    }

    [Fact]
    public void Fact_density_is_tokens_per_sentence()
    {
        NarrativeRenderer.FactDensity([NarrativeRenderer.Cite("[[home.xg]] and [[home.xg]]"), NarrativeRenderer.Cite("[[away.xg]]"), NarrativeRenderer.Cite("None.")]).ShouldBe(1);
        NarrativeRenderer.FactDensity([]).ShouldBe(0);
    }
}
