using System.Security;
using System.Text;
using Pitchside.Agents.Rendering;
using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Agents.Speech;

/// <summary>
/// Assembles a narrative into SSML for the spoken recap (WP10-R1): each sentence rendered by code exactly as the viewer
/// reads it, a 0.4 s pause between sentences, rate <c>+0%</c>, a <c>&lt;prosody&gt;</c> lift on any sentence that
/// cites a goal or the score, and a club or player name wrapped in <c>&lt;sub&gt;</c> only where the pronunciation
/// dictionary gives an alias for the voice's language. Everything else is escaped text.
/// </summary>
public static class SsmlBuilder
{
    public const string Pause = "400ms";
    public const string Rate = "+0%";
    public const string LiftPitch = "+5%";
    public const string LiftVolume = "+10%";

    public static string Build(Narrative narrative, FactSheet sheet, string voice, IReadOnlyDictionary<string, string>? aliases)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(voice);
        var ordered = (aliases ?? new Dictionary<string, string>()).OrderByDescending(a => a.Key.Length).ToList();

        var ssml = new StringBuilder();
        ssml.Append("<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"")
            .Append(LocaleOf(voice, narrative.Language))
            .Append("\"><voice name=\"").Append(SecurityElement.Escape(voice)).Append("\"><prosody rate=\"").Append(Rate).Append("\">");
        for (var i = 0; i < narrative.Sentences.Count; i++)
        {
            if (i > 0)
            {
                ssml.Append("<break time=\"").Append(Pause).Append("\"/>");
            }

            var sentence = narrative.Sentences[i];
            var spoken = Spoken(NarrativeRenderer.RenderSentence(sentence.Text, sheet, narrative.Language).Trim(), ordered);
            if (CitesAGoal(sentence))
            {
                ssml.Append("<prosody pitch=\"").Append(LiftPitch).Append("\" volume=\"").Append(LiftVolume).Append("\">").Append(spoken).Append("</prosody>");
            }
            else
            {
                ssml.Append(spoken);
            }
        }

        return ssml.Append("</prosody></voice></speak>").ToString();
    }

    /// <summary>The voice's locale, such as <c>af-ZA</c> from <c>af-ZA-AdriNeural</c>, or a default for the language.</summary>
    public static string LocaleOf(string voice, string language)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var parts = voice.Split('-');
        return parts.Length >= 3 && parts[0].Length is 2 or 3 && parts[1].Length == 2
            ? $"{parts[0]}-{parts[1]}"
            : language switch
            {
                Languages.Afrikaans => "af-ZA",
                Languages.IsiZulu => "zu-ZA",
                Languages.Spanish => "es-ES",
                _ => "en-ZA",
            };
    }

    /// <summary>A sentence that cites a side's or a player's goals, or the score, is read with a lift.</summary>
    public static bool CitesAGoal(NarrativeSentence sentence)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        return sentence.Citations.Any(key =>
            key is FactKeys.MatchScoreHome or FactKeys.MatchScoreAway || key.EndsWith("." + FactKeys.Goals, StringComparison.Ordinal));
    }

    /// <summary>The sentence escaped for XML, with each name the dictionary knows wrapped in a <c>&lt;sub&gt;</c>.</summary>
    private static string Spoken(string text, List<KeyValuePair<string, string>> aliases)
    {
        var spoken = new StringBuilder();
        var start = 0;
        while (start < text.Length)
        {
            var (at, alias) = NextName(text, start, aliases);
            if (at < 0)
            {
                spoken.Append(SecurityElement.Escape(text[start..]));
                break;
            }

            spoken.Append(SecurityElement.Escape(text[start..at]))
                .Append("<sub alias=\"").Append(SecurityElement.Escape(alias.Value)).Append("\">")
                .Append(SecurityElement.Escape(alias.Key))
                .Append("</sub>");
            start = at + alias.Key.Length;
        }

        return spoken.ToString();
    }

    /// <summary>The earliest whole-word occurrence of a known name at or after <paramref name="from"/>; the longest wins a tie.</summary>
    private static (int At, KeyValuePair<string, string> Alias) NextName(string text, int from, List<KeyValuePair<string, string>> aliases)
    {
        var best = (At: -1, Alias: default(KeyValuePair<string, string>));
        foreach (var alias in aliases)
        {
            for (var at = text.IndexOf(alias.Key, from, StringComparison.Ordinal); at >= 0; at = text.IndexOf(alias.Key, at + 1, StringComparison.Ordinal))
            {
                var end = at + alias.Key.Length;
                var whole = (at == 0 || !char.IsLetterOrDigit(text[at - 1])) && (end == text.Length || !char.IsLetterOrDigit(text[end]));
                if (whole)
                {
                    if (best.At < 0 || at < best.At)
                    {
                        best = (at, alias);
                    }

                    break;
                }
            }
        }

        return best;
    }
}
