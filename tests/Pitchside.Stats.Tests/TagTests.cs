using Pitchside.Contracts;

namespace Pitchside.Stats.Tests;

public sealed class TagTests
{
    private static readonly MatchStatsEngine Engine = new();

    private static List<Tag> TagsOn(int seq)
    {
        var state = Engine.Start(TinyMatch.Document);
        IReadOnlyList<Tag> tags = [];
        foreach (var e in TinyMatch.Events.Take(seq))
        {
            var step = Engine.Apply(state, e);
            state = step.State;
            tags = step.Tags;
        }

        return [.. tags];
    }

    [Fact]
    public void A_hard_completed_pass_raises_pass_hard()
    {
        TagsOn(5).ShouldBe([new Tag(TagNames.PassHard, "HOM-09", TinyMatch.Id(5), 0.6)]);
    }

    [Fact]
    public void A_fast_shot_raises_shot_fast()
    {
        TagsOn(6).ShouldBe([new Tag(TagNames.ShotFast, "HOM-07", TinyMatch.Id(6), 104)]);
    }

    [Fact]
    public void A_slow_shot_raises_nothing()
    {
        TagsOn(16).ShouldBeEmpty();
    }

    [Fact]
    public void Speed_thresholds_fire_once_each_and_together_when_crossed_together()
    {
        TagsOn(13).ShouldBe([new Tag(TagNames.Speed30, "HOM-07", TinyMatch.Id(13), 31)]);
        TagsOn(14).ShouldBe(
        [
            new Tag(TagNames.Speed30, "AWY-10", TinyMatch.Id(14), 34),
            new Tag(TagNames.Speed33, "AWY-10", TinyMatch.Id(14), 34),
        ]);
    }

    [Fact]
    public void Distance_thresholds_fire_when_first_reached()
    {
        var engine = new MatchStatsEngine();
        var state = engine.Start(TinyMatch.Document);
        state = engine.Apply(state, TinyMatch.Events[0]).State;
        var raised = new List<Tag>();
        for (var minute = 1; minute <= 90; minute++)
        {
            var tick = TinyMatch.Events[12] with
            {
                Id = $"T-{minute}",
                Seq = 100 + minute,
                ClockMs = minute * 60_000,
                Tracking = [new TrackingEntry("HOM-01", 120, 12)],
            };
            var step = engine.Apply(state, tick);
            raised.AddRange(step.Tags);
            state = step.State;
        }

        // 120 m a minute: 5 km first reached at minute 42 (5.04 km), 10 km at minute 84 (10.08 km).
        raised.Select(t => (t.Name, t.EventId)).ShouldBe([(TagNames.Distance5, "T-42"), (TagNames.Distance10, "T-84")]);
        engine.Snapshot(state).TextOf(FactKeys.ForPlayer("HOM-01", FactKeys.Tags)).ShouldBe("distance.5,distance.10");
    }

    [Fact]
    public void A_crossing_never_fires_twice()
    {
        var state = Engine.Start(TinyMatch.Document);
        var all = new List<Tag>();
        foreach (var e in TinyMatch.Events)
        {
            var step = Engine.Apply(state, e);
            all.AddRange(step.Tags);
            state = step.State;
        }

        var again = TinyMatch.Events[13] with { Id = "again", Seq = 99 };
        Engine.Apply(state, again).Tags.ShouldBeEmpty();
        all.Count(t => t.Name == TagNames.Speed33).ShouldBe(1);
    }
}
