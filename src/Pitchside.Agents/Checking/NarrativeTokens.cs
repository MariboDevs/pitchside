using System.Text.RegularExpressions;

namespace Pitchside.Agents.Checking;

/// <summary>The <c>[[fact.key]]</c> citation token: the only place a number may appear in a model's text.</summary>
public static partial class NarrativeTokens
{
    /// <summary>The keys cited in a sentence, in order of first appearance.</summary>
    public static IReadOnlyList<string> Keys(string text) =>
        [.. Token().Matches(text).Select(m => m.Groups[1].Value.Trim()).Distinct(StringComparer.Ordinal)];

    /// <summary>Every key written in a sentence, once per token, repeats included.</summary>
    public static IReadOnlyList<string> Written(string text) =>
        [.. Token().Matches(text).Select(m => m.Groups[1].Value.Trim())];

    /// <summary>The text with every token removed, for checks that must ignore cited numbers.</summary>
    public static string Strip(string text) => Token().Replace(text, " ");

    public static string Replace(string text, MatchEvaluator evaluator) => Token().Replace(text, evaluator);

    [GeneratedRegex(@"\[\[([^\[\]]+)\]\]", RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}
