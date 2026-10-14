using System.Collections.Concurrent;
using Pitchside.Engine;
using Pitchside.Moments;
using Pitchside.Stats;

namespace Pitchside.Agents.Tests;

/// <summary>Seeded matches run through the stats engine and the detector, cached per seed.</summary>
internal static class Matches
{
    private static readonly ConcurrentDictionary<long, IReadOnlyList<DetectedMoment>> Cache = new();

    public static IReadOnlyList<DetectedMoment> Moments(long seed) => Cache.GetOrAdd(seed, Detect);

    public static DetectedMoment Moment(long seed, string id) => Moments(seed).Single(m => m.Moment.Id == id);

    private static IReadOnlyList<DetectedMoment> Detect(long seed)
    {
        var match = MatchSimulator.Simulate(seed);
        var stats = new MatchStatsEngine();
        var detector = new MomentDetector();
        var statsState = stats.Start(match.Document);
        var detectorState = detector.Start(match.Document);
        var found = new List<DetectedMoment>();
        foreach (var e in match.Events)
        {
            var step = stats.Apply(statsState, e);
            statsState = step.State;
            var sheet = stats.Snapshot(statsState);
            var (next, moments) = detector.Apply(detectorState, e, sheet, step.Tags);
            detectorState = next;
            found.AddRange(moments.Select(m => new DetectedMoment(m, sheet, match.Document)));
        }

        return found;
    }
}
