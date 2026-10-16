using System.Text.Json;
using System.Xml.Linq;
using Json.Schema;
using Pitchside.Agents.Rendering;
using Pitchside.Agents.Speech;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

public sealed class SsmlBuilderTests
{
    private static readonly XNamespace Ssml = "http://www.w3.org/2001/10/synthesis";
    private static readonly DetectedMoment FullTime = Matches.Moment(7, "MER-7:M:1623:full_time");

    private static Narrative Written(string language, params string[] sentences) => new()
    {
        Id = Narrative.IdFor(FullTime.Moment.Id, Personas.Analyst, language),
        MatchId = "MER-7",
        MomentId = FullTime.Moment.Id,
        Persona = Personas.Analyst,
        Language = language,
        FactSheetSeq = FullTime.Sheet.AsOfSeq,
        Sentences = [.. sentences.Select(NarrativeRenderer.Cite)],
        RenderedText = string.Empty,
        Status = NarrativeStatus.Verified,
        Provenance = new NarrativeProvenance("scripted", "scripted", "persona-analyst-v1", 1, DateTimeOffset.UnixEpoch, false),
        Evaluation = new NarrativeEvaluation(1, 0, null),
    };

    [Fact]
    public void A_narrative_becomes_one_voice_with_a_pause_between_sentences_at_the_plain_rate()
    {
        var narrative = Written(Languages.Afrikaans, "Dit eindig [[match.score.home]] tot [[match.score.away]].", "Besit was [[home.possessionShare]].");

        var document = XDocument.Parse(SsmlBuilder.Build(narrative, FullTime.Sheet, "af-ZA-AdriNeural", null));

        var speak = document.Root!;
        speak.Name.ShouldBe(Ssml + "speak");
        speak.Attribute(XNamespace.Xml + "lang")!.Value.ShouldBe("af-ZA");
        var voice = speak.Element(Ssml + "voice")!;
        voice.Attribute("name")!.Value.ShouldBe("af-ZA-AdriNeural");
        var prosody = voice.Element(Ssml + "prosody")!;
        prosody.Attribute("rate")!.Value.ShouldBe(SsmlBuilder.Rate);
        prosody.Elements(Ssml + "break").Select(b => b.Attribute("time")!.Value).ShouldBe([SsmlBuilder.Pause]);
    }

    [Fact]
    public void Numbers_are_rendered_by_code_in_the_narratives_language()
    {
        var narrative = Written(Languages.Afrikaans, "Die verwagte doele was [[home.xg]].");
        var expected = NarrativeRenderer.RenderSentence("[[home.xg]]", FullTime.Sheet, Languages.Afrikaans);

        var ssml = SsmlBuilder.Build(narrative, FullTime.Sheet, "af-ZA-AdriNeural", null);

        expected.ShouldContain(",");
        ssml.ShouldContain($"Die verwagte doele was {expected}.");
        ssml.ShouldNotContain("[[");
    }

    [Fact]
    public void Only_a_sentence_citing_a_goal_or_the_score_is_lifted()
    {
        var narrative = Written(Languages.English, "It ends [[match.score.home]] to [[match.score.away]].", "Possession was [[home.possessionShare]].");

        var prosody = XDocument.Parse(SsmlBuilder.Build(narrative, FullTime.Sheet, "en-ZA-LeahNeural", null)).Root!
            .Element(Ssml + "voice")!.Element(Ssml + "prosody")!;

        var lifted = prosody.Elements(Ssml + "prosody").ToList();
        lifted.Count.ShouldBe(1);
        lifted[0].Value.ShouldStartWith("It ends ");
        lifted[0].Attribute("pitch")!.Value.ShouldBe(SsmlBuilder.LiftPitch);
        SsmlBuilder.CitesAGoal(NarrativeRenderer.Cite("Kim has [[player.p1.goals]] today.")).ShouldBeTrue();
        SsmlBuilder.CitesAGoal(NarrativeRenderer.Cite("They had [[home.shots]] shots.")).ShouldBeFalse();
    }

    [Fact]
    public void A_name_gets_an_alias_only_where_the_dictionary_has_one_for_the_voices_language()
    {
        var narrative = Written(Languages.English, "Inyoni Wanderers beat Amanzi United and Harbourside Athletic watched.");
        var aliases = Crew.Catalog.Pronunciations[Languages.English];

        var subs = XDocument.Parse(SsmlBuilder.Build(narrative, FullTime.Sheet, "en-ZA-LeahNeural", aliases)).Descendants(Ssml + "sub").ToList();

        subs.Select(s => s.Value).ShouldBe(["Inyoni Wanderers", "Amanzi United"]);
        subs.ShouldAllBe(s => s.Attribute("alias")!.Value == aliases[s.Value]);
    }

    [Fact]
    public void A_name_inside_a_longer_word_is_not_aliased_and_markup_characters_are_escaped()
    {
        var narrative = Written(Languages.English, "Marula Cityside & <friends> cheered.");

        var ssml = SsmlBuilder.Build(narrative, FullTime.Sheet, "en-ZA-LeahNeural", Crew.Catalog.Pronunciations[Languages.English]);

        var document = XDocument.Parse(ssml);
        document.Descendants(Ssml + "sub").ShouldBeEmpty();
        document.Root!.Value.ShouldContain("Marula Cityside & <friends> cheered.");
    }

    [Theory]
    [InlineData("zu-ZA-ThandoNeural", "zu", "zu-ZA")]
    [InlineData("es-ES-ElviraNeural", "es", "es-ES")]
    [InlineData("custom", "af", "af-ZA")]
    [InlineData("custom", "en", "en-ZA")]
    public void The_locale_comes_from_the_voice_or_else_the_language(string voice, string language, string locale) =>
        SsmlBuilder.LocaleOf(voice, language).ShouldBe(locale);

    [Fact]
    public void Every_aliased_name_is_a_club_in_the_league()
    {
        var clubs = Pitchside.Engine.MeridianLeague.Clubs.Select(c => c.Club.Name).ToHashSet();

        Crew.Catalog.Pronunciations.Values.SelectMany(d => d.Keys).ShouldAllBe(name => clubs.Contains(name));
    }

    [Fact]
    public async Task Every_spoken_seed_7_recap_validates_against_the_schema()
    {
        var schema = JsonSchema.FromText(File.ReadAllText(RepositoryRoot.Combine("schemas", "recap.schema.json")));
        var context = await Crew.FullWorkflow(recaps: Crew.Recaps()).RunAsync(FullTime.WorkItem, CancellationToken.None);

        context.Recaps.Count.ShouldBe(4);
        foreach (var spoken in context.Recaps)
        {
            var json = JsonSerializer.Serialize(spoken.Recap, ContractJson.Options);
            using var document = JsonDocument.Parse(json);
            schema.Evaluate(document.RootElement).IsValid.ShouldBeTrue(json);
            XDocument.Parse(spoken.Recap.Ssml).Root!.Name.ShouldBe(Ssml + "speak");
            JsonSerializer.Deserialize<Recap>(json, ContractJson.Options).ShouldBe(spoken.Recap);
        }
    }
}
