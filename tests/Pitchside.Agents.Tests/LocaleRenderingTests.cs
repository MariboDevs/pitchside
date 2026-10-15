using Pitchside.Agents.Rendering;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

public sealed class LocaleRenderingTests
{
    private static readonly DetectedMoment Goal = Matches.Moment(7, "MER-7:M:0324:goal");

    [Theory]
    [InlineData(Languages.English, "0.63")]
    [InlineData(Languages.IsiZulu, "0.63")]
    [InlineData(Languages.Afrikaans, "0,63")]
    [InlineData(Languages.Spanish, "0,63")]
    public void Decimals_use_the_language_separator(string language, string expected) =>
        NarrativeRenderer.Format(Goal.Sheet.Find("away.xg")!, language).ShouldBe(expected);

    [Theory]
    [InlineData(Languages.English)]
    [InlineData(Languages.Afrikaans)]
    [InlineData(Languages.IsiZulu)]
    [InlineData(Languages.Spanish)]
    public void Whole_numbers_are_the_same_in_every_language(string language) =>
        NarrativeRenderer.Format(Goal.Sheet.Find("away.shots")!, language).ShouldBe(NarrativeRenderer.Format(Goal.Sheet.Find("away.shots")!));

    [Fact]
    public void A_sentence_renders_every_token_in_the_language_format()
    {
        var sentence = NarrativeRenderer.Cite("Inyoni Wanderers het [[away.xg]] verwagte doele uit [[away.shots]] skote.");

        var text = NarrativeRenderer.Render([sentence], Goal.Sheet, Languages.Afrikaans);

        text.ShouldBe("Inyoni Wanderers het 0,63 verwagte doele uit 4 skote.");
    }
}
