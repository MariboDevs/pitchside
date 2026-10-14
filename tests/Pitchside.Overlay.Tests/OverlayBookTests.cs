using Pitchside.Contracts;
using Pitchside.Moments;

namespace Pitchside.Overlay.Tests;

public sealed class OverlayBookTests
{
    private static readonly (Moment Moment, FactSheet Sheet) Goal = Fixture.First(MomentTypes.Goal);

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
        var aired = book.Air("MER-7", entry.Id, entry.Period, entry.OutAtMs + 10_000)!;
        aired.State.ShouldBe(OverlayState.Scheduled);
        aired.InAtMs.ShouldBe(entry.OutAtMs + 12_000);
        (aired.OutAtMs - aired.InAtMs).ShouldBe(entry.OutAtMs - entry.InAtMs);
        book.Air("MER-7", entry.Id, entry.Period, 0).ShouldBeNull();
        book.Hold("MER-7", "nope", 1, 0).ShouldBeNull();
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
