using Pitchside.Agents.Providers;

namespace Pitchside.Agents.Tests;

public sealed class PromptCatalogTests
{
    [Theory]
    [InlineData("explainer", "explainer-v1")]
    [InlineData("factcheck", "factcheck-v1")]
    public void Prompts_resolve_to_their_latest_version(string name, string version)
    {
        var prompt = Crew.Catalog.Get(name);
        prompt.VersionLabel.ShouldBe(version);
        prompt.Text.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void The_explainer_prompt_shows_the_token_rule_three_times() =>
        System.Text.RegularExpressions.Regex.Count(Crew.Catalog.Get("explainer").Text, @"\[\[[a-zA-Z0-9.\-]+\]\]").ShouldBeGreaterThanOrEqualTo(3);

    [Fact]
    public void An_unknown_prompt_throws() => Should.Throw<KeyNotFoundException>(() => Crew.Catalog.Get("translator"));

    [Fact]
    public void The_forbidden_list_holds_the_required_words()
    {
        foreach (var word in new[] { "odds", "bet", "bookmaker", "wager" })
        {
            Crew.Catalog.ForbiddenWords.ShouldContain(word);
        }

        Crew.Catalog.ForbiddenWords.ShouldNotContain(w => w.StartsWith('#'));
    }

    [Fact]
    public void Scripted_answers_exist_for_both_prompts()
    {
        var scripts = new ScriptedChatProvider().ScriptNames;
        scripts.ShouldContain("explainer.default");
        scripts.ShouldContain("factcheck.default");
    }
}
