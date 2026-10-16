using System.Text.Json.Nodes;
using Pitchside.Contracts;

namespace Pitchside.Agents.Prompting;

/// <summary>
/// The translator's user message: the target language, the English sentences with their tokens, and the names that
/// must not be translated. The sentences are also offered as the variable <c>{{sentencesJson}}</c>, which is how the
/// scripted provider answers offline: it returns the English unchanged.
/// </summary>
public static class TranslationInput
{
    public static string LanguageName(string language) => language switch
    {
        Languages.Afrikaans => "Afrikaans",
        Languages.IsiZulu => "isiZulu",
        Languages.Spanish => "Spanish",
        _ => "English",
    };

    public static string Build(Narrative english, string language, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(english);
        ArgumentNullException.ThrowIfNull(names);
        var sentences = new JsonArray([.. english.Sentences.Select(s => (JsonNode)new JsonObject { ["text"] = s.Text })]);
        return new JsonObject
        {
            ["language"] = language,
            ["languageName"] = LanguageName(language),
            ["persona"] = english.Persona,
            ["sentences"] = new JsonArray([.. english.Sentences.Select(s => (JsonNode)JsonValue.Create(s.Text))]),
            ["names"] = new JsonArray([.. names.Select(n => (JsonNode)JsonValue.Create(n))]),
            ["variables"] = new JsonObject { ["sentencesJson"] = sentences.ToJsonString(ExplainerInput.Serialization) },
        }.ToJsonString(ExplainerInput.Serialization);
    }
}
