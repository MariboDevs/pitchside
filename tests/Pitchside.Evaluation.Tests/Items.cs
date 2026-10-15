using Pitchside.Contracts;

namespace Pitchside.Evaluation.Tests;

/// <summary>A small moment, sheet and narrative built by hand, so each score can be worked out on paper.</summary>
internal static class Items
{
    public static readonly FactSheet Sheet = new("MER-7", 120, 1, 600_000,
    [
        new Fact { Key = "home.xg", Value = FactValue.Of(1.24), Unit = "xg", Precision = 2, DerivedFrom = ["MER-7-00100"] },
        new Fact { Key = "match.score.home", Value = FactValue.Of(1), Unit = "goals", Precision = 0, DerivedFrom = ["MER-7-00110"] },
        new Fact { Key = "away.shots", Value = FactValue.Of(3), Precision = 0, DerivedFrom = ["MER-7-00090"] },
    ]);

    public static readonly Moment Moment = new()
    {
        Id = "MER-7:M:0120:goal",
        MatchId = "MER-7",
        Type = "goal",
        Period = 1,
        ClockMs = 600_000,
        TriggerSeq = 120,
        FactSheetSeq = 120,
        Priority = 100,
        Window = new MomentWindow(100, 120),
        SideId = "AMZ",
        PlayerIds = [],
        Headline = "Goal",
        Evidence = new MomentEvidence(["MER-7-00110"], ["home.xg", "match.score.home"]),
    };

    public static Narrative With(params NarrativeSentence[] sentences) => new()
    {
        Id = Narrative.IdFor(Moment.Id, Personas.Analyst, Languages.English),
        MatchId = "MER-7",
        MomentId = Moment.Id,
        Persona = Personas.Analyst,
        Language = Languages.English,
        FactSheetSeq = 120,
        Sentences = sentences,
        RenderedText = "Amanzi lead 1-0 with 1.24 xG.",
        Status = NarrativeStatus.Verified,
        Provenance = new NarrativeProvenance("scripted", "scripted", "persona-analyst-v1", 1, DateTimeOffset.UnixEpoch, false),
        Evaluation = new NarrativeEvaluation(1, 1, null),
    };
}
