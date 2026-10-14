using System.Globalization;
using System.Text.RegularExpressions;
using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Agents.Checking;

/// <summary>
/// The deterministic fact-check (WP04-R3). It runs before any model check and its verdict on numbers is final:
/// every token resolves, no digit appears outside a token, every name is in the match document, every key is in
/// the moment's scope, and nothing forbidden is said. Passed only with zero objections.
/// </summary>
public sealed partial class CodeFactChecker
{
    private readonly IReadOnlyList<Regex> _forbidden;

    public CodeFactChecker(IEnumerable<string> forbiddenWords)
    {
        ArgumentNullException.ThrowIfNull(forbiddenWords);
        _forbidden = [.. forbiddenWords.Select(w => new Regex($@"\b{Regex.Escape(w)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))];
    }

    public Verdict Check(IReadOnlyList<NarrativeSentence> sentences, FactSheet sheet, Moment moment, MatchDocument document, string language = Languages.English)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(document);

        var objections = new List<Objection>();
        if (sentences.Count == 0)
        {
            objections.Add(new Objection(ObjectionKinds.WholeDraft, ObjectionKinds.InvalidOutput, "The draft has no sentences."));
        }

        var names = KnownNameWords(document);
        var players = moment.PlayerIds.ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < sentences.Count; i++)
        {
            var index = i.ToString(CultureInfo.InvariantCulture);
            var text = sentences[i].Text ?? string.Empty;

            foreach (var key in NarrativeTokens.Keys(text))
            {
                if (sheet.Find(key) is null)
                {
                    objections.Add(new Objection(index, ObjectionKinds.UnknownFact, $"[[{key}]] is not on the fact sheet."));
                }

                if (ScopeProblem(key, players) is { } problem)
                {
                    objections.Add(new Objection(index, ObjectionKinds.WrongScope, problem));
                }
            }

            var stripped = NarrativeTokens.Strip(text);
            var withoutShirts = ShirtNumber().Replace(stripped, " ");
            if (Digit().Match(withoutShirts) is { Success: true } digit)
            {
                objections.Add(new Objection(index, ObjectionKinds.UncitedNumber,
                    $"'{NumberAround(withoutShirts, digit.Index)}' is a number outside a [[key]] token."));
            }

            if (language == Languages.English)
            {
                foreach (var word in UnknownNames(stripped, names))
                {
                    objections.Add(new Objection(index, ObjectionKinds.ForbiddenContent, $"'{word}' is not a name in the match document."));
                }
            }

            foreach (var forbidden in _forbidden)
            {
                if (forbidden.Match(stripped) is { Success: true } hit)
                {
                    objections.Add(new Objection(index, ObjectionKinds.ForbiddenContent, $"'{hit.Value}' is forbidden vocabulary."));
                }
            }
        }

        return new Verdict(objections);
    }

    private static string? ScopeProblem(string key, IReadOnlySet<string> players)
    {
        var scope = key.Split('.', 2)[0];
        if (scope is FactKeys.MatchScope or FactKeys.HomeScope or FactKeys.AwayScope)
        {
            return null;
        }

        if (scope != FactKeys.PlayerScope)
        {
            return $"[[{key}]] has an unknown scope '{scope}'.";
        }

        var parts = key.Split('.');
        if (parts.Length < 3)
        {
            return $"[[{key}]] does not name a player.";
        }

        var playerId = string.Join('.', parts[1..^1]);
        return players.Contains(playerId) ? null : $"[[{key}]] is about {playerId}, who is not part of this moment.";
    }

    /// <summary>Capitalised words that are neither in the document nor ordinary sentence openers.</summary>
    private static IEnumerable<string> UnknownNames(string text, IReadOnlySet<string> names)
    {
        foreach (Match sentence in SentenceSpan().Matches(text))
        {
            var first = true;
            foreach (Match word in Word().Matches(sentence.Value))
            {
                var w = word.Value;
                var opening = first;
                first = false;
                if (!char.IsUpper(w[0]) || names.Contains(w) || SentenceStarters.Anywhere.Contains(w))
                {
                    continue;
                }

                if (opening && SentenceStarters.English.Contains(w))
                {
                    continue;
                }

                yield return w;
            }
        }
    }

    private static HashSet<string> KnownNameWords(MatchDocument document)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<string> sources =
        [
            document.Competition, document.Venue,
            document.Home.Club.Name, document.Home.Club.ShortName, document.Away.Club.Name, document.Away.Club.ShortName,
            .. document.Home.Starters.Concat(document.Home.Bench).Concat(document.Away.Starters).Concat(document.Away.Bench).Select(p => p.Name),
        ];
        foreach (var source in sources)
        {
            foreach (Match word in Word().Matches(source))
            {
                words.Add(word.Value);
            }
        }

        return words;
    }

    private static string NumberAround(string text, int index)
    {
        var start = index;
        var end = index;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        return text[start..end];
    }

    [GeneratedRegex(@"#[0-9]{1,2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex ShirtNumber();

    [GeneratedRegex(@"\p{Nd}", RegexOptions.CultureInvariant)]
    private static partial Regex Digit();

    [GeneratedRegex(@"\p{L}[\p{L}'\-]*", RegexOptions.CultureInvariant)]
    private static partial Regex Word();

    [GeneratedRegex(@"[^.!?]+[.!?]*", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceSpan();
}
