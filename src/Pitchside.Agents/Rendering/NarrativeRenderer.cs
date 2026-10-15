using System.Globalization;
using Pitchside.Agents.Checking;
using Pitchside.Contracts;

namespace Pitchside.Agents.Rendering;

/// <summary>
/// Produces the text a viewer reads by substituting every <c>[[key]]</c> token with the fact's value at its
/// precision, formatted for the narrative's language. This is the only path from a number on the fact sheet to a
/// number on screen.
/// </summary>
public static class NarrativeRenderer
{
    private static readonly NumberFormatInfo DecimalPoint = CultureInfo.InvariantCulture.NumberFormat;
    private static readonly NumberFormatInfo DecimalComma = new() { NumberDecimalSeparator = ",", NegativeSign = "-" };

    public static string Render(IReadOnlyList<NarrativeSentence> sentences, FactSheet sheet, string language = Languages.English)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        ArgumentNullException.ThrowIfNull(sheet);
        return string.Join(' ', sentences.Select(s => RenderSentence(s.Text, sheet, language).Trim()));
    }

    public static string RenderSentence(string text, FactSheet sheet, string language = Languages.English)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sheet);
        return NarrativeTokens.Replace(text, match =>
        {
            var key = match.Groups[1].Value.Trim();
            var fact = sheet.Find(key) ?? throw new InvalidOperationException($"Cannot render [[{key}]]: it is not on the fact sheet.");
            return Format(fact, language);
        });
    }

    /// <summary>
    /// A fact's value as shown: numbers at the fact's precision with the language's decimal separator (a comma for
    /// Afrikaans and Spanish, a point for English and isiZulu), no grouping; text as is. The separators are fixed
    /// here rather than read from the operating system's culture data, so every machine renders the same text.
    /// </summary>
    public static string Format(Fact fact, string language = Languages.English)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Value.Number is { } number
            ? number.ToString("F" + fact.Precision.ToString(CultureInfo.InvariantCulture), NumberFormat(language))
            : fact.Value.Text ?? string.Empty;
    }

    public static NumberFormatInfo NumberFormat(string language) =>
        language is Languages.Afrikaans or Languages.Spanish ? DecimalComma : DecimalPoint;

    /// <summary>
    /// The share of written <c>[[key]]</c> tokens that resolve on the fact sheet (WP07-R1). A narrative with no token
    /// has nothing unresolved, so it scores 1; a verified narrative always scores 1, since the checker rejects any
    /// token that does not resolve.
    /// </summary>
    public static double CitationCoverage(IReadOnlyList<NarrativeSentence> sentences, FactSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        ArgumentNullException.ThrowIfNull(sheet);
        var written = sentences.SelectMany(s => NarrativeTokens.Written(s.Text)).ToList();
        return written.Count == 0 ? 1 : (double)written.Count(key => sheet.Find(key) is not null) / written.Count;
    }

    /// <summary>Tokens written per sentence (WP07-R1); 0 for a narrative with no sentences.</summary>
    public static double FactDensity(IReadOnlyList<NarrativeSentence> sentences)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        return sentences.Count == 0 ? 0 : (double)sentences.Sum(s => NarrativeTokens.Written(s.Text).Count) / sentences.Count;
    }

    /// <summary>A sentence with its citations taken from its tokens, which are the authority.</summary>
    public static NarrativeSentence Cite(string text) => new(text, NarrativeTokens.Keys(text));
}
