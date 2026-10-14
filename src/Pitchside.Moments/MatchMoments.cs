using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments;

/// <summary>Runs the stats engine and the detector together over a whole match.</summary>
public static class MatchMoments
{
    public static IReadOnlyList<Moment> Detect(MatchDocument document, IEnumerable<MatchEvent> events, IMatchStatsEngine? stats = null, IMomentDetector? detector = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(events);
        stats ??= new MatchStatsEngine();
        detector ??= new MomentDetector();
        var statsState = stats.Start(document);
        var detectorState = detector.Start(document);
        var moments = new List<Moment>();
        foreach (var e in events)
        {
            var step = stats.Apply(statsState, e);
            statsState = step.State;
            var (next, found) = detector.Apply(detectorState, e, stats.Snapshot(statsState), step.Tags);
            detectorState = next;
            moments.AddRange(found);
        }

        return moments;
    }
}
