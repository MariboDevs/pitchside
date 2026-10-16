using Microsoft.Extensions.Options;
using Pitchside.Agents.Speech;
using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Runtime.Recaps;
using Pitchside.Runtime.Usage;

namespace Pitchside.Agents.Tests;

public sealed class RecapTests
{
    private const string FullTimeId = "MER-7:M:1623:full_time";
    private static readonly DetectedMoment FullTime = Matches.Moment(7, FullTimeId);
    private static readonly DetectedMoment Goal = Matches.Moment(7, "MER-7:M:0324:goal");

    /// <summary>The full crew's full-time context with speech off, for tests that drive the writer directly.</summary>
    private static async Task<MomentContext> FullTimeAsync() => await Crew.FullWorkflow().RunAsync(FullTime.WorkItem, CancellationToken.None);

    [Fact]
    public async Task Full_time_is_spoken_in_every_language_from_the_analyst_variant()
    {
        var store = new InMemoryRecapStore();

        var context = await Crew.FullWorkflow(recaps: Crew.Recaps(store)).RunAsync(FullTime.WorkItem, CancellationToken.None);

        context.Recaps.Select(r => r.Recap.Language).ShouldBe(Languages.All, ignoreOrder: true);
        foreach (var spoken in context.Recaps)
        {
            var recap = spoken.Recap;
            recap.NarrativeId.ShouldBe(Narrative.IdFor(FullTimeId, Personas.Analyst, recap.Language));
            recap.SpokenLanguage.ShouldBe(recap.Language);
            recap.EnglishFallback.ShouldBeFalse();
            recap.Voice.ShouldBe(new SpeechOptions().Voices[recap.Language]);
            recap.Ssml.ShouldStartWith("<speak ");
            spoken.Audio.Take(2).ShouldBe(new byte[] { 0xFF, 0xFB });
            (await store.GetAsync("MER-7", recap.Language, CancellationToken.None)).ShouldBe(recap);
            (await store.GetAudioAsync("MER-7", recap.Language, CancellationToken.None)).ShouldBe(spoken.Audio);
        }
    }

    [Fact]
    public async Task Any_other_moment_and_speech_off_write_no_recap()
    {
        var speech = new CountingSpeechProvider();

        (await Crew.FullWorkflow(recaps: Crew.Recaps(speech: speech)).RunAsync(Goal.WorkItem, CancellationToken.None)).Recaps.ShouldBeEmpty();
        var off = Crew.Recaps(speech: speech, options: new SpeechOptions { Provider = SpeechOptions.NoneProvider });
        (await Crew.FullWorkflow(recaps: off).RunAsync(FullTime.WorkItem, CancellationToken.None)).Recaps.ShouldBeEmpty();

        speech.Spoken.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_language_without_a_voice_gets_the_english_audio_which_is_spoken_once()
    {
        var speech = new CountingSpeechProvider();
        var options = new SpeechOptions { Provider = SpeechOptions.ScriptedProvider };
        options.Voices.Remove(Languages.IsiZulu);

        var recaps = await Crew.Recaps(speech: speech, options: options).WriteAsync(await FullTimeAsync(), CancellationToken.None);

        var zulu = recaps.Single(r => r.Recap.Language == Languages.IsiZulu);
        var english = recaps.Single(r => r.Recap.Language == Languages.English);
        zulu.Recap.EnglishFallback.ShouldBeTrue();
        zulu.Recap.SpokenLanguage.ShouldBe(Languages.English);
        zulu.Recap.Voice.ShouldBe(english.Recap.Voice);
        zulu.Recap.Ssml.ShouldBe(english.Recap.Ssml);
        speech.Spoken.Count.ShouldBe(3);
    }

    [Fact]
    public async Task A_translation_that_fell_back_to_english_is_spoken_in_english()
    {
        var context = await FullTimeAsync();
        var fellBack = context.Narratives
            .Select(n => n.Language == Languages.Afrikaans ? n with { Status = NarrativeStatus.Fallback } : n)
            .ToList();

        var recaps = await Crew.Recaps().WriteAsync(context with { Narratives = [.. fellBack] }, CancellationToken.None);

        var afrikaans = recaps.Single(r => r.Recap.Language == Languages.Afrikaans).Recap;
        afrikaans.EnglishFallback.ShouldBeTrue();
        afrikaans.NarrativeId.ShouldBe(Narrative.IdFor(FullTimeId, Personas.Analyst, Languages.English));
    }

    [Fact]
    public async Task Without_an_analyst_variant_the_base_narrative_is_spoken()
    {
        var context = await Crew.Workflow().RunAsync(FullTime.WorkItem, CancellationToken.None);

        var recaps = await Crew.Recaps().WriteAsync(context, CancellationToken.None);

        recaps.Single().Recap.NarrativeId.ShouldBe(Narrative.IdFor(FullTimeId, Personas.Base, Languages.English));
    }

    [Fact]
    public async Task A_voice_that_fails_skips_its_language_and_the_rest_are_still_spoken()
    {
        var speech = new CountingSpeechProvider(failFor: new SpeechOptions().Voices[Languages.Spanish]);

        var recaps = await Crew.Recaps(speech: speech).WriteAsync(await FullTimeAsync(), CancellationToken.None);

        recaps.Select(r => r.Recap.Language).ShouldBe([Languages.English, Languages.Afrikaans, Languages.IsiZulu], ignoreOrder: true);
    }

    [Fact]
    public async Task Past_the_hourly_spoken_line_limit_a_recap_is_skipped_and_its_lines_are_given_back()
    {
        var context = await FullTimeAsync();
        var lines = context.Narratives.Single(n => n.Id == Narrative.IdFor(FullTimeId, Personas.Analyst, Languages.English)).Sentences.Count;
        var guard = new UsageGuard(Options.Create(new UsageGuardOptions { SpokenLinesPerHour = lines + 1 }), TimeProvider.System, enabled: true);

        var recaps = await Crew.Recaps(guard: guard).WriteAsync(context, CancellationToken.None);

        recaps.Select(r => r.Recap.Language).ShouldBe([Languages.English]);
        guard.Snapshot("caller").Single(u => u.Name == UsageLimits.SpokenLines).Used.ShouldBe(lines);
    }
}
