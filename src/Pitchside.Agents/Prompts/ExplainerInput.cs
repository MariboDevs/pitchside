using System.Text.Json;
using System.Text.Json.Nodes;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Agents.Prompts;

/// <summary>
/// The user messages the crew sends: the moment, the subset of the fact sheet it may cite (the evidence keys,
/// every <c>match.*</c> fact, the side's facts and the involved players' facts) and the names it may use.
/// Values are shown so the model can reason; it must still write each number as its <c>[[key]]</c> token.
/// </summary>
public static class ExplainerInput
{
    public static string Build(MomentContext context, IReadOnlyList<Objection>? fixThese = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var message = Base(context);
        if (fixThese is { Count: > 0 })
        {
            message["fixThese"] = new JsonArray([.. fixThese.Select(o => (JsonNode)JsonValue.Create($"sentence {o.SentenceIndex}: {o.Kind}: {o.Detail}"))]);
        }

        return message.ToJsonString(Serialization);
    }

    public static string BuildFactCheck(MomentContext context, IReadOnlyList<NarrativeSentence> sentences)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sentences);
        var message = Base(context);
        message["sentences"] = new JsonArray([.. sentences.Select(s => (JsonNode)JsonValue.Create(s.Text))]);
        return message.ToJsonString(Serialization);
    }

    /// <summary>
    /// The facts the explainer may cite: the evidence keys, every <c>match.*</c> fact, the side's facts (both sides
    /// when the moment has none) and the involved players' facts, in sheet order.
    /// </summary>
    public static IReadOnlyList<Fact> Subset(Moment moment, FactSheet sheet, string? sideScope)
    {
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(sheet);
        var evidence = moment.Evidence.FactKeys.ToHashSet(StringComparer.Ordinal);
        List<string> prefixes = [FactKeys.MatchScope + ".", .. moment.PlayerIds.Select(id => $"{FactKeys.PlayerScope}.{id}.")];
        prefixes.AddRange(sideScope is null ? [FactKeys.HomeScope + ".", FactKeys.AwayScope + "."] : [sideScope + "."]);
        return [.. sheet.Facts.Where(f => evidence.Contains(f.Key) || prefixes.Any(p => f.Key.StartsWith(p, StringComparison.Ordinal)))];
    }

    internal static JsonObject Base(MomentContext context)
    {
        var variables = TemplateVariables.FromMoment(context.Moment, context.Document);
        var subset = Subset(context.Moment, context.FactSheet, variables.GetValueOrDefault("side"));

        var moment = context.Moment;
        return new JsonObject
        {
            ["moment"] = new JsonObject
            {
                ["id"] = moment.Id,
                ["type"] = moment.Type,
                ["headline"] = moment.Headline,
                ["side"] = variables.GetValueOrDefault("side"),
                ["period"] = moment.Period,
                ["playerIds"] = new JsonArray([.. moment.PlayerIds.Select(p => (JsonNode)JsonValue.Create(p))]),
            },
            ["facts"] = new JsonArray([.. subset.Select(f => (JsonNode)new JsonObject
            {
                ["key"] = f.Key,
                ["value"] = f.Value.Number is { } n ? JsonValue.Create(n) : JsonValue.Create(f.Value.Text),
                ["unit"] = f.Unit,
            })]),
            ["names"] = new JsonObject
            {
                ["home"] = context.Document.Home.Club.Name,
                ["away"] = context.Document.Away.Club.Name,
                ["players"] = new JsonArray([.. moment.PlayerIds.Select((id, i) => (JsonNode)new JsonObject
                {
                    ["id"] = id,
                    ["name"] = variables[$"player{i}Name"],
                })]),
            },
            ["variables"] = new JsonObject([.. variables.Select(v => KeyValuePair.Create(v.Key, (JsonNode?)JsonValue.Create(v.Value)))]),
        };
    }

    internal static readonly JsonSerializerOptions Serialization = new() { WriteIndented = false };
}
