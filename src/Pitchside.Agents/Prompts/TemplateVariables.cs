using System.Text.Json;
using System.Text.RegularExpressions;
using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Agents.Prompts;

/// <summary>
/// The placeholders a fallback template or a scripted answer may use: <c>{{homeName}}</c>, <c>{{awayName}}</c>,
/// <c>{{side}}</c> (the moment's side scope, <c>home</c> or <c>away</c>), <c>{{sideName}}</c>, <c>{{opponent}}</c>,
/// <c>{{opponentName}}</c>, and for each player on the moment <c>{{player0}}</c> (id) and <c>{{player0Name}}</c>.
/// </summary>
public static partial class TemplateVariables
{
    public static IReadOnlyDictionary<string, string> FromMoment(Moment moment, MatchDocument document)
    {
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(document);
        var names = document.Home.Starters.Concat(document.Home.Bench).Concat(document.Away.Starters).Concat(document.Away.Bench)
            .ToDictionary(p => p.Id, p => p.Name, StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["homeName"] = document.Home.Club.Name,
            ["awayName"] = document.Away.Club.Name,
        };

        if (moment.SideId is { } sideId)
        {
            var home = sideId == document.Home.Club.Id;
            values["side"] = home ? FactKeys.HomeScope : FactKeys.AwayScope;
            values["sideName"] = home ? document.Home.Club.Name : document.Away.Club.Name;
            values["opponent"] = home ? FactKeys.AwayScope : FactKeys.HomeScope;
            values["opponentName"] = home ? document.Away.Club.Name : document.Home.Club.Name;
        }

        for (var i = 0; i < moment.PlayerIds.Count; i++)
        {
            values[$"player{i}"] = moment.PlayerIds[i];
            values[$"player{i}Name"] = names.GetValueOrDefault(moment.PlayerIds[i], moment.PlayerIds[i]);
        }

        return values;
    }

    /// <summary>Reads the same variables from the <c>variables</c> object of an explainer user message.</summary>
    public static IReadOnlyDictionary<string, string> FromUserMessage(JsonElement message)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (message.TryGetProperty("variables", out var variables))
        {
            foreach (var property in variables.EnumerateObject())
            {
                values[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return values;
    }

    /// <summary>Replaces every <c>{{name}}</c>; a placeholder with no value is a template bug and throws.</summary>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        return Placeholder().Replace(template, match => values.TryGetValue(match.Groups[1].Value, out var value)
            ? value
            : throw new KeyNotFoundException($"Template placeholder '{match.Value}' has no value for this moment."));
    }

    [GeneratedRegex(@"\{\{([A-Za-z0-9]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
