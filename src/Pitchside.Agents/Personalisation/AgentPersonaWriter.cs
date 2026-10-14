using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Executors;
using Pitchside.Agents.Prompts;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Personalisation;

/// <summary>
/// Writes every persona variant of a verified base narrative in parallel (WP06-R1). Each goes through its shape rules
/// (sentence count, and for the casual fan the word and number limits), then the same checks as the base, and is
/// retried once with the objections. A variant that still fails is kept as <see cref="NarrativeStatus.Rejected"/>
/// for the control room; viewers of that persona see the base narrative instead.
/// </summary>
public sealed partial class AgentPersonaWriter(
    IChatProvider provider,
    PromptCatalog catalog,
    NarrativeVerifier verifier,
    IOptions<AgentsOptions> options,
    TimeProvider time) : IPersonaWriter
{
    public const int MaxAttempts = 2;

    public async Task<IReadOnlyList<Narrative>> WriteAsync(MomentContext context, Narrative baseNarrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(baseNarrative);
        var subjects = PersonaSubjects.For(context, options.Value.Personas);
        return await Task.WhenAll(subjects.Select(s => WriteAsync(context, baseNarrative, s, cancellationToken))).ConfigureAwait(false);
    }

    /// <summary>The objections a draft earns from its persona's shape alone, before any fact is checked.</summary>
    public static IReadOnlyList<Objection> ShapeObjections(PersonaSpec spec, IReadOnlyList<NarrativeSentence> sentences)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(sentences);
        var objections = new List<Objection>();
        if (sentences.Count < spec.MinSentences || sentences.Count > spec.MaxSentences)
        {
            objections.Add(new Objection(ObjectionKinds.WholeDraft, ObjectionKinds.InvalidOutput,
                $"The {spec.Persona} persona writes {spec.MinSentences} to {spec.MaxSentences} sentences, not {sentences.Count}."));
        }

        for (var i = 0; i < sentences.Count; i++)
        {
            var words = Word().Count(sentences[i].Text);
            if (spec.MaxWords is { } maxWords && words > maxWords)
            {
                objections.Add(new Objection(i.ToString(System.Globalization.CultureInfo.InvariantCulture), ObjectionKinds.InvalidOutput,
                    $"The sentence has {words} words; the {spec.Persona} persona allows {maxWords}."));
            }
        }

        var numbers = sentences.Sum(s => NarrativeTokens.Keys(s.Text).Count);
        if (spec.MaxNumbers is { } maxNumbers && numbers > maxNumbers)
        {
            objections.Add(new Objection(ObjectionKinds.WholeDraft, ObjectionKinds.InvalidOutput,
                $"The text carries {numbers} numbers; the {spec.Persona} persona allows {maxNumbers}."));
        }

        return objections;
    }

    private async Task<Narrative> WriteAsync(MomentContext context, Narrative baseNarrative, PersonaSubject subject, CancellationToken cancellationToken)
    {
        var spec = subject.Spec;
        var prompt = catalog.Get(spec.PromptName);
        IReadOnlySet<string>? named = spec.Persona == Personas.PlayerFocus
            ? context.Moment.PlayerIds.ToHashSet(StringComparer.Ordinal)
            : null;

        IReadOnlyList<Objection>? fixThese = null;
        IReadOnlyList<NarrativeSentence> sentences = [];
        var model = provider.Name;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var request = new ChatRequest(spec.PromptName, prompt.VersionLabel, prompt.Text, PersonaInput.Build(context, baseNarrative, subject, fixThese), DeploymentTier.Fast, context.Moment, attempt);
            try
            {
                var result = await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
                model = result.Model;
                sentences = ExplainerExecutor.Parse(result.Text);
            }
            catch (Exception ex) when (ProviderFailure.IsRecoverable(ex))
            {
                fixThese = [new Objection(ObjectionKinds.WholeDraft, ObjectionKinds.InvalidOutput, ex.Message)];
                continue;
            }

            var shape = ShapeObjections(spec, sentences);
            var verdict = shape.Count > 0
                ? new Verdict(shape)
                : await verifier.VerifyAsync(context, sentences, Languages.English, named, cancellationToken).ConfigureAwait(false);
            if (verdict.Passed)
            {
                return NarrativeFactory.Create(context, spec.Persona, Languages.English, sentences, NarrativeStatus.Verified, Provenance(model, prompt, attempt), subject.SubjectId);
            }

            fixThese = verdict.Objections;
        }

        return NarrativeFactory.Create(context, spec.Persona, Languages.English, sentences, NarrativeStatus.Rejected, Provenance(model, prompt, MaxAttempts), subject.SubjectId);
    }

    private NarrativeProvenance Provenance(string model, Prompt prompt, int attempts) =>
        new(provider.Name, model, prompt.VersionLabel, attempts, time.GetUtcNow(), false);

    [GeneratedRegex(@"\S+", RegexOptions.CultureInvariant)]
    private static partial Regex Word();
}
