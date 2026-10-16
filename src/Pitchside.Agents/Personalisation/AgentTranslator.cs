using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Executors;
using Pitchside.Agents.Prompting;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;

namespace Pitchside.Agents.Personalisation;

/// <summary>
/// Translates every English narrative into the other configured languages in parallel (WP06-R1). A translation must
/// return the same tokens the same number of times, keep every name that the English text uses, and pass the same
/// checks as the base; otherwise that language falls back to the English sentences, marked as a fallback, so the
/// viewer always gets a verified card. A translation also keeps its persona's shape. A rejected English variant is
/// not translated: each language gets it as rejected too, so a viewer of that language is told to fall back to the
/// base rather than wait. Numbers are rendered afterwards in the language's format, and each result is published as
/// it completes (WP06-R3).
/// </summary>
public sealed partial class AgentTranslator(
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
        var work = english.SelectMany(n => languages.Select(async l =>
        {
            var translated = await TranslateAsync(context, n, l, cancellationToken).ConfigureAwait(false);
            await context.PublishAsync(translated, cancellationToken).ConfigureAwait(false);
            return translated;
        }));
        return await Task.WhenAll(work).ConfigureAwait(false);
    }

    /// <summary>
    /// Why a translation cannot stand in for its English source, or nothing when it can: every token written the same
    /// number of times, and every word of a name (a surname or a club's short form as much as a full name) kept at
    /// least as often as the English uses it.
    /// </summary>
    public static IReadOnlyList<string> Mismatches(IReadOnlyList<NarrativeSentence> source, IReadOnlyList<NarrativeSentence> translation, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(translation);
        ArgumentNullException.ThrowIfNull(names);
        var problems = new List<string>();
        var expected = source.SelectMany(s => NarrativeTokens.Written(s.Text)).Order(StringComparer.Ordinal).ToList();
        var actual = translation.SelectMany(s => NarrativeTokens.Written(s.Text)).Order(StringComparer.Ordinal).ToList();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            problems.Add($"The tokens changed: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
        }

        var english = NarrativeTokens.Strip(string.Join(' ', source.Select(s => s.Text)));
        var translated = NarrativeTokens.Strip(string.Join(' ', translation.Select(s => s.Text)));
        var words = names.SelectMany(n => NameWord().Matches(n).Select(m => m.Value)).Where(w => w.Length > 1).Distinct(StringComparer.Ordinal);
        foreach (var word in words)
        {
            var pattern = new Regex($@"(?<!\p{{L}}){Regex.Escape(word)}(?!\p{{L}})", RegexOptions.CultureInvariant);
            if (pattern.Count(translated) < pattern.Count(english))
            {
                problems.Add($"The name '{word}' was translated or dropped.");
            }
        }

        // Every word surviving is not enough: "Nathan Kim and Femi Zielinski" must not come back as "Nathan Zielinski
        // and Femi Kim", so each full name must appear in the translation exactly as often as in the English, as a
        // phrase: not split, swapped or dropped, and not repeated or added either.
        foreach (var name in names.Where(n => NameWord().Count(n) > 1).Distinct(StringComparer.Ordinal))
        {
            var phrase = string.Join(@"\s+", NameWord().Matches(name).Select(m => Regex.Escape(m.Value)));
            var pattern = new Regex($@"(?<!\p{{L}}){phrase}(?!\p{{L}})", RegexOptions.CultureInvariant);
            var want = pattern.Count(english);
            var got = pattern.Count(translated);
            if (got < want)
            {
                problems.Add($"The name '{name}' was split, swapped or dropped.");
            }
            else if (got > want)
            {
                problems.Add($"The name '{name}' was repeated or added.");
            }
        }

        return problems;
    }

    private async Task<Narrative> TranslateAsync(MomentContext context, Narrative english, string language, CancellationToken cancellationToken)
    {
        var prompt = catalog.Get(PromptName);
        if (english.Status == NarrativeStatus.Rejected)
        {
            return NarrativeFactory.Create(context, english.Persona, language, english.Sentences, NarrativeStatus.Rejected, Provenance(provider.Name, prompt, false), english.Subject);
        }

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
            if (Mismatches(english.Sentences, sentences, names).Count > 0
                || (english.Persona != Personas.Base && AgentPersonaWriter.ShapeObjections(PersonaSpecs.For(english.Persona), sentences).Count > 0))
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
        document.Home.Club.Name, document.Away.Club.Name, document.Home.Club.ShortName, document.Away.Club.ShortName,
        .. document.Home.Starters.Concat(document.Home.Bench).Concat(document.Away.Starters).Concat(document.Away.Bench).Select(p => p.Name),
    ];

    [GeneratedRegex(@"\p{L}[\p{L}'-]*", RegexOptions.CultureInvariant)]
    private static partial Regex NameWord();
}
