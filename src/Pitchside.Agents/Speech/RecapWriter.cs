using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Prompts;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Moments;
using Pitchside.Runtime.Recaps;
using Pitchside.Runtime.Usage;

namespace Pitchside.Agents.Speech;

/// <summary>
/// Speaks the full-time recap in every viewer language (WP-10). For each language it reads the analyst variant (the
/// base narrative when there is none), assembled into SSML by <see cref="SsmlBuilder"/>, with that language's
/// configured voice. A language with no voice, or whose translation fell back to English, gets the English audio and
/// says so. Each spoken sentence counts against <c>UsageGuard:SpokenLinesPerHour</c>; audio that is reused is not
/// counted twice. A recap that cannot be spoken or stored is logged and skipped: speech never fails the moment.
/// </summary>
public sealed partial class RecapWriter(
    PromptCatalog catalog,
    IRecapStore store,
    IOptions<SpeechOptions> options,
    TimeProvider time,
    ILogger<RecapWriter> logger,
    ISpeechProvider? speech = null,
    UsageGuard? guard = null)
{
    public bool Enabled => speech is not null && options.Value.Enabled;

    public async Task<ImmutableList<RecapAudio>> WriteAsync(MomentContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enabled || context.Moment.Type != MomentTypes.FullTime || ChooseEnglish(context) is not { } english)
        {
            return [];
        }

        var voices = options.Value.Voices;
        if (!voices.TryGetValue(Languages.English, out var englishVoice) || string.IsNullOrWhiteSpace(englishVoice))
        {
            LogNoEnglishVoice(context.Moment.Id);
            return [];
        }

        var spoken = new Dictionary<string, (string Ssml, byte[] Audio)>(StringComparer.Ordinal);
        var recaps = ImmutableList.CreateBuilder<RecapAudio>();
        var languages = context.Narratives.Select(n => n.Language).Prepend(Languages.English).Distinct(StringComparer.Ordinal);
        foreach (var language in languages)
        {
            var own = language == Languages.English ? english : ChooseTranslated(context, language);
            var narrative = own is not null && voices.TryGetValue(language, out var ownVoice) && !string.IsNullOrWhiteSpace(ownVoice) ? own : english;
            var voice = narrative == english ? englishVoice : voices[language];
            try
            {
                if (!spoken.TryGetValue(narrative.Id, out var audio))
                {
                    var ssml = SsmlBuilder.Build(narrative, context.FactSheet, voice, catalog.Pronunciations.GetValueOrDefault(narrative.Language));
                    if (!TakeLines(narrative.Sentences.Count))
                    {
                        LogLimitReached(context.Moment.Id, language);
                        continue;
                    }

                    audio = spoken[narrative.Id] = (ssml, await speech!.SynthesizeAsync(ssml, cancellationToken).ConfigureAwait(false));
                }

                var recap = new Recap
                {
                    MatchId = context.Moment.MatchId,
                    MomentId = context.Moment.Id,
                    NarrativeId = narrative.Id,
                    Language = language,
                    SpokenLanguage = narrative.Language,
                    Voice = voice,
                    EnglishFallback = language != Languages.English && narrative.Language == Languages.English,
                    Ssml = audio.Ssml,
                    CreatedUtc = time.GetUtcNow(),
                };
                await store.SaveAsync(recap, audio.Audio, cancellationToken).ConfigureAwait(false);
                recaps.Add(new RecapAudio(recap, audio.Audio));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogRecapFailed(ex, context.Moment.Id, language);
            }
        }

        return recaps.ToImmutable();
    }

    /// <summary>The English narrative to speak: the analyst variant when it is verified, otherwise the base.</summary>
    private static Narrative? ChooseEnglish(MomentContext context) =>
        Analyst(context, Languages.English)
        ?? (context.BaseNarrative is { Status: NarrativeStatus.Verified or NarrativeStatus.Fallback } baseNarrative ? baseNarrative : null);

    /// <summary>A translation counts only when it is verified: a fallback translation is English text.</summary>
    private static Narrative? ChooseTranslated(MomentContext context, string language) =>
        Analyst(context, language)
        ?? context.Narratives.FirstOrDefault(n => n.Persona == Personas.Base && n.Language == language && n.Status == NarrativeStatus.Verified);

    private static Narrative? Analyst(MomentContext context, string language) =>
        context.Narratives.FirstOrDefault(n => n.Persona == Personas.Analyst && n.Subject is null && n.Language == language && n.Status == NarrativeStatus.Verified);

    /// <summary>Counts one spoken line per sentence; when the hour's limit is reached, gives back what this recap took.</summary>
    private bool TakeLines(int lines)
    {
        if (guard is null)
        {
            return true;
        }

        var taken = new List<long>();
        for (var i = 0; i < lines; i++)
        {
            var decision = guard.TryTake(UsageLimits.SpokenLines, UsageGuard.HostKey);
            if (!decision.Allowed)
            {
                foreach (var window in taken)
                {
                    guard.Release(UsageLimits.SpokenLines, UsageGuard.HostKey, window);
                }

                return false;
            }

            taken.Add(decision.Window);
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moment {MomentId}: no English voice is configured (Speech:Voices:en), so no recap is spoken")]
    private partial void LogNoEnglishVoice(string momentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moment {MomentId}: the hourly spoken-line limit is reached, so the {Language} recap is skipped")]
    private partial void LogLimitReached(string momentId, string language);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moment {MomentId}: the {Language} recap could not be spoken or stored")]
    private partial void LogRecapFailed(Exception exception, string momentId, string language);
}
