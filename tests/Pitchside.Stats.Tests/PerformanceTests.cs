using System.Diagnostics;

namespace Pitchside.Stats.Tests;

public sealed class PerformanceTests
{
    [Fact]
    public void The_whole_sample_snapshots_at_every_event_in_under_two_seconds()
    {
        var engine = new MatchStatsEngine();
        var state = engine.Start(SampleMatch.Document);
        var facts = 0;
        var stopwatch = Stopwatch.StartNew();
        foreach (var e in SampleMatch.Events)
        {
            state = engine.Apply(state, e).State;
            facts += engine.Snapshot(state).Facts.Count;
        }

        stopwatch.Stop();
        facts.ShouldBeGreaterThan(0);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }
}
