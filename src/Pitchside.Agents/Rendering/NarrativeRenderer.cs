using System.Globalization;
using Pitchside.Agents.Checking;
using Pitchside.Contracts;

namespace Pitchside.Agents.Rendering;

/// <summary>
/// Produces the text a viewer reads by substituting every <c>[[key]]</c> token with the fact's value at its
/// precision. This is the only path from a number on the fact sheet to a number on screen.
/// </summary>
public static class NarrativeRenderer
{
    public static string Render(IReadOnlyList<NarrativeSentence> sentences, FactSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        ArgumentNullException.ThrowIfNull(sheet);
        return string.Join(' ', sentences.Select(s => RenderSentence(s.Text, sheet).Trim()));
    }

    public static string RenderSentence(string text, FactSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sheet);
        return NarrativeTokens.Replace(text, match =>
        {
            var key = match.Groups[1].Value.Trim();
            var fact = sheet.Find(key) ?? throw new InvalidOperationException($"Cannot render [[{key}]]: it is not on the fact sheet.");
            return Format(fact);
        });
    }

    /// <summary>A fact's value as shown: numbers at the fact's precision, invariant culture; text as is.</summary>
    public static string Format(Fact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Value.Number is { } number
            ? number.ToString("F" + fact.Precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : fact.Value.Text ?? string.Empty;
    }

    /// <summary>The share of sentences citing at least one fact.</summary>
    public static double CitationCoverage(IReadOnlyList<NarrativeSentence> sentences)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        return sentences.Count == 0 ? 0 : (double)sentences.Count(s => s.Citations.Count > 0) / sentences.Count;
    }

    /// <summary>A sentence with its citations taken from its tokens, which are the authority.</summary>
    public static NarrativeSentence Cite(string text) => new(text, NarrativeTokens.Keys(text));
}
