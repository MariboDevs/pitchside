using Pitchside.Contracts;
using Pitchside.Engine;
using Pitchside.Moments;
using Pitchside.Stats;

namespace Pitchside.Overlay.Tests;

/// <summary>Seed 7's moments with their sheets, and narratives of a chosen length for the scheduler to place.</summary>
internal static class Fixture
{
    private static readonly Lazy<(MatchDocument Document, List<(Moment Moment, FactSheet Sheet)> Moments)> Data = new(Build);

    public static MatchDocument Document => Data.Value.Document;

    public static IReadOnlyList<(Moment Moment, FactSheet Sheet)> Moments => Data.Value.Moments;

    public static (Moment Moment, FactSheet Sheet) First(string type) => Moments.First(m => m.Moment.Type == type);

    public static Moment At(Moment moment, int clockMs, int priority) => moment with { ClockMs = clockMs, Priority = priority, Id = $"MER-7:M:{clockMs / 1000:0000}:{moment.Type}" };

    public static Narrative NarrativeFor(Moment moment, int words) => new()
    {
        Id = Narrative.IdFor(moment.Id, Personas.Base, Languages.English),
        MatchId = moment.MatchId,
        MomentId = moment.Id,
        Persona = Personas.Base,
        Language = Languages.English,
        FactSheetSeq = moment.FactSheetSeq,
        Sentences = [],
        RenderedText = string.Join(' ', Enumerable.Repeat("word", words)),
        Status = NarrativeStatus.Verified,
        Provenance = new NarrativeProvenance("scripted", "scripted", "explainer-v1", 1, DateTimeOffset.UnixEpoch, false),
        Evaluation = new NarrativeEvaluation(1, 1, null),
    };

    private static (MatchDocument, List<(Moment, FactSheet)>) Build()
    {
        var match = MatchSimulator.Simulate(7);
        var stats = new MatchStatsEngine();
        var detector = new MomentDetector();
        var s = stats.Start(match.Document);
        var d = detector.Start(match.Document);
        var found = new List<(Moment, FactSheet)>();
        foreach (var e in match.Events)
        {
            var step = stats.Apply(s, e);
            s = step.State;
            var sheet = stats.Snapshot(s);
            var (next, moments) = detector.Apply(d, e, sheet, step.Tags);
            d = next;
            found.AddRange(moments.Select(m => (m, sheet)));
        }

        return (match.Document, found);
    }
}
