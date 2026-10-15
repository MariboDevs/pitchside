using System.Diagnostics;

namespace Pitchside.Stats.Tests;

/// <summary>
/// The whole sample snapshotted at every event must take under two seconds on CI hardware. The events are loaded
/// and the code paths warmed before the clock starts, and the test runs alone so other tests do not share its CPU.
/// </summary>
[Collection(PerformanceCollection.Name)]
public sealed class PerformanceTests
{
    [Fact]
    [Trait("Category", "Timing")]
    public void The_whole_sample_snapshots_at_every_event_in_under_two_seconds()
    {
        var document = SampleMatch.Document;
        var events = SampleMatch.Events.ToList();
        var engine = new MatchStatsEngine();

        var warm = engine.Start(document);
        foreach (var e in events.Take(200))
        {
            warm = engine.Apply(warm, e).State;
            engine.Snapshot(warm);
        }

        var state = engine.Start(document);
        var facts = 0;
        var stopwatch = Stopwatch.StartNew();
        foreach (var e in events)
        {
            state = engine.Apply(state, e).State;
            facts += engine.Snapshot(state).Facts.Count;
        }

        stopwatch.Stop();
        facts.ShouldBeGreaterThan(0);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }
}
