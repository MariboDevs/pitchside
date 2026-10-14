using Microsoft.Extensions.Options;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Executors;
using Pitchside.Agents.Prompts;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Personalisation;

/// <summary>
/// Translates every English narrative into the other configured languages in parallel (WP06-R1). A translation must
/// return the same tokens the same number of times, keep every name that the English text uses, and pass the same
/// checks as the base; otherwise that language falls back to the English sentences, marked as a fallback, so the
/// viewer always gets a verified card. Numbers are rendered afterwards in the language's format.
/// </summary>
public sealed class AgentTranslator(
    IChatProvider provider,
    PromptCatalog catalog,
    NarrativeVerifier verifier,
    IOptions<AgentsOptions> options,
    TimeProvider time) : ITranslator
{
    public const string PromptName = "translator";

    public async Task<IReadOnlyList<Narrative>> TranslateAsync(MomentContext context, IReadOnlyList<Narrative> english, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(english);
        var languages = options.Value.Languages.Where(l => l != Languages.English && Languages.All.Contains(l)).Distinct(StringComparer.Ordinal).ToList();
        var work = english.SelectMany(n => languages.Select(l => TranslateAsync(context, n, l, cancellationToken)));
        return await Task.WhenAll(work).ConfigureAwait(false);
    }

    /// <summary>Why a translation cannot stand in for its English source, or nothing when it can.</summary>
    public static IReadOnlyList<string> Mismatches(IReadOnlyList<NarrativeSentence> source, IReadOnlyList<NarrativeSentence> translation, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(translation);
        ArgumentNullException.ThrowIfNull(names);
        var problems = new List<string>();
        var expected = source.SelectMany(s => NarrativeTokens.Keys(s.Text)).Order(StringComparer.Ordinal).ToList();
        var actual = translation.SelectMany(s => NarrativeTokens.Keys(s.Text)).Order(StringComparer.Ordinal).ToList();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            problems.Add($"The tokens changed: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
        }

        var english = string.Join(' ', source.Select(s => s.Text));
        var translated = string.Join(' ', translation.Select(s => s.Text));
        foreach (var name in names.Where(n => english.Contains(n, StringComparison.Ordinal) && !translated.Contains(n, StringComparison.Ordinal)))
        {
            problems.Add($"The name '{name}' was translated or dropped.");
        }

        return problems;
    }

    private async Task<Narrative> TranslateAsync(MomentContext context, Narrative english, string language, CancellationToken cancellationToken)
    {
        var prompt = catalog.Get(PromptName);
        return await TryTranslateAsync(context, english, language, prompt, cancellationToken).ConfigureAwait(false)
            ?? NarrativeFactory.Create(context, english.Persona, language, english.Sentences, NarrativeStatus.Fallback, Provenance(provider.Name, prompt, true), english.Subject);
    }

    /// <summary>The checked translation, or null when the call failed or the answer cannot stand in for the English.</summary>
    private async Task<Narrative?> TryTranslateAsync(MomentContext context, Narrative english, string language, Prompt prompt, CancellationToken cancellationToken)
    {
        var names = Names(context.Document);
        var request = new ChatRequest(PromptName, prompt.VersionLabel, prompt.Text, TranslationInput.Build(english, language, names), DeploymentTier.Fast, context.Moment, 1);
        try
        {
            var result = await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            var sentences = ExplainerExecutor.Parse(result.Text);
            if (Mismatches(english.Sentences, sentences, names).Count > 0)
            {
                return null;
            }

            IReadOnlySet<string>? named = english.Persona == Personas.PlayerFocus ? context.Moment.PlayerIds.ToHashSet(StringComparer.Ordinal) : null;
            var verdict = await verifier.VerifyAsync(context, sentences, language, named, cancellationToken).ConfigureAwait(false);
            return verdict.Passed
                ? NarrativeFactory.Create(context, english.Persona, language, sentences, english.Status, Provenance(result.Model, prompt, false), english.Subject)
                : null;
        }
        catch (Exception ex) when (ProviderFailure.IsRecoverable(ex))
        {
            return null;
        }
    }

    private NarrativeProvenance Provenance(string model, Prompt prompt, bool fallback) =>
        new(provider.Name, model, prompt.VersionLabel, 1, time.GetUtcNow(), fallback);

    private static List<string> Names(MatchDocument document) =>
    [
        document.Home.Club.Name, document.Away.Club.Name,
        .. document.Home.Starters.Concat(document.Home.Bench).Concat(document.Away.Starters).Concat(document.Away.Bench).Select(p => p.Name),
    ];
}
