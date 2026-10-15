using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Agents.Checking;

/// <summary>
/// The deterministic fact-check (WP04-R3). It runs before any model check and its verdict on numbers is final:
/// every sentence cites at least one fact, every token resolves, no digit appears outside a token, every name is
/// a name in the match document, every key is in the moment's scope, and nothing forbidden is said. Passed only
/// with zero objections.
/// </summary>
public sealed partial class CodeFactChecker
{
    private readonly HashSet<string> _forbidden;

    /// <param name="forbiddenHashes">SHA-256 of each forbidden word, lower case, as from <see cref="Hash"/>.</param>
    public CodeFactChecker(IEnumerable<string> forbiddenHashes)
    {
        ArgumentNullException.ThrowIfNull(forbiddenHashes);
        _forbidden = new HashSet<string>(forbiddenHashes, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The form a forbidden word is committed in: lower-case hex SHA-256 of the lower-cased word.</summary>
    public static string Hash(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(word.ToLowerInvariant())));
    }

    /// <param name="namedPlayerIds">
    /// When set, the only players the text may name; naming any other player of either squad is forbidden content.
    /// The player-focus persona uses it to stay on the followed player and those directly involved.
    /// </param>
    public Verdict Check(
        IReadOnlyList<NarrativeSentence> sentences,
        FactSheet sheet,
        Moment moment,
        MatchDocument document,
        string language = Languages.English,
        IReadOnlySet<string>? namedPlayerIds = null)
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

        var names = KnownNames(document);
        var roster = document.Home.Starters.Concat(document.Home.Bench).Concat(document.Away.Starters).Concat(document.Away.Bench).ToList();
        var players = moment.PlayerIds.ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < sentences.Count; i++)
        {
            var index = i.ToString(CultureInfo.InvariantCulture);
            var text = sentences[i].Text ?? string.Empty;
            var keys = NarrativeTokens.Keys(text).ToList();
            if (keys.Count == 0)
            {
                objections.Add(new Objection(index, ObjectionKinds.UncitedSentence, "The sentence cites no fact."));
            }

            foreach (var key in keys)
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
            var withoutShirts = ShirtNumber().Replace(stripped, m => IsNamedShirt(m, stripped, roster) ? " " : m.Value);
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

            if (namedPlayerIds is not null)
            {
                foreach (var player in MentionedPlayers(stripped, roster).Where(p => !namedPlayerIds.Contains(p.Id)).DistinctBy(p => p.Id))
                {
                    objections.Add(new Objection(index, ObjectionKinds.ForbiddenContent, $"'{player.Name}' is not a player this text may name."));
                }
            }

            foreach (Match word in Word().Matches(stripped))
            {
                if (_forbidden.Contains(Hash(word.Value)))
                {
                    objections.Add(new Objection(index, ObjectionKinds.ForbiddenContent, $"'{word.Value}' is forbidden vocabulary."));
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

    /// <summary>
    /// Every player a run of name words could refer to. Each full name inside the run resolves to its player; each word
    /// of the run not part of a full name resolves to every player whose name has that word, so a given name or a
    /// shared surname counts as a mention of each of them and is refused unless all of them may be named. Matching
    /// inside the run, not the run as a whole, keeps a name found when the run also holds other capitalised words,
    /// such as a translated sentence's first word ("Ante Hugo Sato").
    /// </summary>
    private static IEnumerable<Player> MentionedPlayers(string text, IReadOnlyList<Player> roster)
    {
        var names = roster.Select(p => (Player: p, Words: Word().Matches(p.Name).Select(w => w.Value).ToArray())).ToList();
        foreach (var run in NameRuns(text))
        {
            var covered = new bool[run.Count];
            foreach (var (player, words) in names.Where(n => n.Words.Length > 1))
            {
                for (var start = 0; start + words.Length <= run.Count; start++)
                {
                    if (words.Select((w, i) => string.Equals(w, run[start + i], StringComparison.Ordinal)).All(match => match))
                    {
                        Array.Fill(covered, true, start, words.Length);
                        yield return player;
                    }
                }
            }

            for (var i = 0; i < run.Count; i++)
            {
                if (covered[i])
                {
                    continue;
                }

                foreach (var (player, words) in names.Where(n => n.Words.Contains(run[i], StringComparer.Ordinal)))
                {
                    yield return player;
                }
            }
        }
    }

    /// <summary>
    /// A shirt number such as <c>#9</c> is allowed only when a player who wears it is named in the same sentence, by
    /// full name or by any single word of it.
    /// </summary>
    private static bool IsNamedShirt(Match shirt, string sentence, IReadOnlyList<Player> roster)
    {
        var number = int.Parse(shirt.Groups["number"].Value, CultureInfo.InvariantCulture);
        var words = Word().Matches(sentence).Select(w => w.Value).ToHashSet(StringComparer.Ordinal);
        return roster.Any(p => p.ShirtNumber == number && Word().Matches(p.Name).Any(w => words.Contains(w.Value)));
    }

    /// <summary>
    /// Runs of adjacent capitalised words, ordinary sentence words aside, that are not a contiguous part of one name
    /// in the document. "Nathan Kim" and "Kim" pass; "Nathan" from one name followed by "Zielinski" from another
    /// does not, because the run is checked as one phrase.
    /// </summary>
    private static IEnumerable<string> UnknownNames(string text, IReadOnlyList<string[]> names) =>
        NameRuns(text).Select(run => Unknown(run, names)).OfType<string>();

    /// <summary>Each run of adjacent capitalised words in the text, ordinary sentence words aside, sentence by sentence.</summary>
    private static IEnumerable<List<string>> NameRuns(string text)
    {
        foreach (Match sentence in SentenceSpan().Matches(text))
        {
            var run = new List<string>();
            var previousEnd = -1;
            var first = true;
            foreach (Match word in Word().Matches(sentence.Value))
            {
                var w = word.Value;
                var opening = first;
                first = false;
                var adjacent = previousEnd >= 0 && string.IsNullOrWhiteSpace(sentence.Value[previousEnd..word.Index]);
                previousEnd = word.Index + word.Length;
                var nameWord = char.IsUpper(w[0]) && !SentenceStarters.Anywhere.Contains(w) && !(opening && SentenceStarters.English.Contains(w));
                if ((!nameWord || !adjacent) && run.Count > 0)
                {
                    yield return run;
                    run = [];
                }

                if (nameWord)
                {
                    run.Add(w);
                }
            }

            if (run.Count > 0)
            {
                yield return run;
            }
        }
    }

    private static string? Unknown(List<string> run, IReadOnlyList<string[]> names)
    {
        if (run.Count == 0)
        {
            return null;
        }

        foreach (var name in names)
        {
            for (var start = 0; start + run.Count <= name.Length; start++)
            {
                if (run.Select((w, i) => string.Equals(w, name[start + i], StringComparison.Ordinal)).All(match => match))
                {
                    return null;
                }
            }
        }

        return string.Join(' ', run);
    }

    /// <summary>Every name in the document as its sequence of words: competition, venue, clubs and players.</summary>
    private static List<string[]> KnownNames(MatchDocument document)
    {
        IEnumerable<string> sources =
        [
            document.Competition, document.Venue,
            document.Home.Club.Name, document.Home.Club.ShortName, document.Away.Club.Name, document.Away.Club.ShortName,
            .. document.Home.Starters.Concat(document.Home.Bench).Concat(document.Away.Starters).Concat(document.Away.Bench).Select(p => p.Name),
        ];
        return [.. sources.Select(source => Word().Matches(source).Select(w => w.Value).ToArray())];
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

    [GeneratedRegex(@"#(?<number>[0-9]{1,2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex ShirtNumber();

    [GeneratedRegex(@"\p{Nd}", RegexOptions.CultureInvariant)]
    private static partial Regex Digit();

    [GeneratedRegex(@"\p{L}[\p{L}'\-]*", RegexOptions.CultureInvariant)]
    private static partial Regex Word();

    [GeneratedRegex(@"[^.!?]+[.!?]*", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceSpan();
}
