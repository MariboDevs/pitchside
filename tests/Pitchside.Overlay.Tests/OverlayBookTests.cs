using Pitchside.Contracts;
using Pitchside.Moments;

namespace Pitchside.Overlay.Tests;

public sealed class OverlayBookTests
{
    private static readonly (Moment Moment, FactSheet Sheet) Goal = Fixture.First(MomentTypes.Goal);
    private static readonly (Moment Moment, FactSheet Sheet) Swing = Fixture.First(MomentTypes.MomentumSwing);

    [Fact]
    public void A_moment_is_scheduled_once()
    {
        var book = new OverlayBook();
        book.Schedule(Goal.Moment, Fixture.NarrativeFor(Goal.Moment, 5), Goal.Sheet, Fixture.Document).Count.ShouldBe(1);
        book.Schedule(Goal.Moment, Fixture.NarrativeFor(Goal.Moment, 5), Goal.Sheet, Fixture.Document).ShouldBeEmpty();
        book.Entries("MER-7").ShouldHaveSingleItem();
    }

    [Fact]
    public void Hold_then_air_releases_a_card_from_the_current_clock()
    {
        var book = new OverlayBook();
        var entry = book.Schedule(Goal.Moment, Fixture.NarrativeFor(Goal.Moment, 5), Goal.Sheet, Fixture.Document).Single();

        book.Hold("MER-7", entry.Id, entry.Period, entry.OutAtMs).ShouldBeNull();
        book.Hold("MER-7", entry.Id, entry.Period, entry.InAtMs)!.State.ShouldBe(OverlayState.Held);
        var aired = book.Air("MER-7", entry.Id, entry.Period, entry.OutAtMs + 10_000).ShouldHaveSingleItem();
        aired.State.ShouldBe(OverlayState.Scheduled);
        aired.InAtMs.ShouldBe(entry.OutAtMs + 12_000);
        (aired.OutAtMs - aired.InAtMs).ShouldBe(entry.OutAtMs - entry.InAtMs);
        book.Air("MER-7", entry.Id, entry.Period, 0).ShouldBeEmpty();
        book.Hold("MER-7", "nope", 1, 0).ShouldBeNull();
    }

    [Fact]
    public void Two_held_cards_released_together_never_overlap()
    {
        var book = new OverlayBook();
        book.SetMode("MER-7", ApprovalMode.Approve);
        var goal = book.Schedule(Goal.Moment, Fixture.NarrativeFor(Goal.Moment, 5), Goal.Sheet, Fixture.Document).Single();
        var swing = book.Schedule(Swing.Moment with { ClockMs = Goal.Moment.ClockMs, Period = Goal.Moment.Period }, Fixture.NarrativeFor(Swing.Moment, 5), Swing.Sheet, Fixture.Document).Single();

        book.Air("MER-7", goal.Id, goal.Period, Goal.Moment.ClockMs);
        book.Air("MER-7", swing.Id, swing.Period, Goal.Moment.ClockMs);

        var line = book.Entries("MER-7").Where(e => e.State == OverlayState.Scheduled).OrderBy(e => e.InAtMs).ToList();
        line.Count.ShouldBe(2);
        line[1].InAtMs.ShouldBeGreaterThanOrEqualTo(line[0].OutAtMs + OverlayScheduler.ExitMs);
    }

    [Fact]
    public void A_restarted_book_reads_its_stored_timeline_back()
    {
        var first = new OverlayBook();
        var entry = first.Schedule(Goal.Moment, Fixture.NarrativeFor(Goal.Moment, 5), Goal.Sheet, Fixture.Document).Single();

        var restarted = new OverlayBook();
        restarted.IsLoaded("MER-7").ShouldBeFalse();
        restarted.Load("MER-7", first.Entries("MER-7"));

        restarted.IsLoaded("MER-7").ShouldBeTrue();
        restarted.Entries("MER-7").ShouldHaveSingleItem().ShouldBe(entry);
        restarted.Hold("MER-7", entry.Id, entry.Period, entry.InAtMs)!.State.ShouldBe(OverlayState.Held);
    }

    [Fact]
    public void Approve_mode_applies_to_new_cards()
    {
        var book = new OverlayBook();
        book.SetMode("MER-7", ApprovalMode.Approve);
        book.Mode("MER-7").ShouldBe(ApprovalMode.Approve);
        book.Schedule(Goal.Moment, Fixture.NarrativeFor(Goal.Moment, 5), Goal.Sheet, Fixture.Document).Single().State.ShouldBe(OverlayState.Held);
        book.Mode("MER-8").ShouldBe(ApprovalMode.Auto);
    }
}
