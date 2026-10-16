using System.Text.Json.Nodes;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Personalisation;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Prompting;

/// <summary>
/// The persona writer's user message: everything the explainer saw, with both sides' facts, plus the verified base
/// sentences it rewrites and the viewer it writes for (the club or the player). The subject is also offered as the
/// variables <c>{{subject}}</c>, <c>{{subjectName}}</c> and <c>{{subjectSide}}</c>.
/// </summary>
public static class PersonaInput
{
    public static string Build(MomentContext context, Narrative baseNarrative, PersonaSubject subject, IReadOnlyList<Objection>? fixThese = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(baseNarrative);
        ArgumentNullException.ThrowIfNull(subject);
        var message = ExplainerInput.Base(context);
        message["facts"] = new JsonArray([.. ExplainerInput.Subset(context.Moment, context.FactSheet, null).Select(f => (JsonNode)new JsonObject
        {
            ["key"] = f.Key,
            ["value"] = f.Value.Number is { } n ? JsonValue.Create(n) : JsonValue.Create(f.Value.Text),
            ["unit"] = f.Unit,
        })]);
        message["persona"] = subject.Spec.Persona;
        message["baseSentences"] = new JsonArray([.. baseNarrative.Sentences.Select(s => (JsonNode)JsonValue.Create(s.Text))]);
        if (subject.SubjectId is { } id)
        {
            var player = subject.Spec.Persona == Personas.PlayerFocus;
            message["viewer"] = new JsonObject
            {
                [player ? "playerId" : "clubId"] = id,
                [player ? "playerName" : "clubName"] = subject.SubjectName,
                ["side"] = subject.SubjectSide,
            };
            var variables = (JsonObject)message["variables"]!;
            variables["subject"] = id;
            variables["subjectName"] = subject.SubjectName;
            variables["subjectSide"] = subject.SubjectSide;
        }

        if (fixThese is { Count: > 0 })
        {
            message["fixThese"] = new JsonArray([.. fixThese.Select(o => (JsonNode)JsonValue.Create($"sentence {o.SentenceIndex}: {o.Kind}: {o.Detail}"))]);
        }

        return message.ToJsonString(ExplainerInput.Serialization);
    }
}
