using Pitchside.Contracts;
using Pitchside.Moments;

namespace Pitchside.Overlay.Tests;

public sealed class OverlaySchedulerTests
{
    private static readonly (Moment Moment, FactSheet Sheet) Goal = Fixture.First(MomentTypes.Goal);
    private static readonly (Moment Moment, FactSheet Sheet) Swing = Fixture.First(MomentTypes.MomentumSwing);

    private static IReadOnlyList<OverlayEntry> Place(IReadOnlyList<OverlayEntry> existing, Moment moment, int words = 10, ApprovalMode mode = ApprovalMode.Auto) =>
        OverlayScheduler.Schedule(existing, moment, Fixture.NarrativeFor(moment, words), Goal.Sheet, Fixture.Document, mode);

    [Theory]
    [InlineData(0, 6_000)]
    [InlineData(10, 8_500)]
    [InlineData(24, 12_000)]
    [InlineData(40, 12_000)]
    public void A_card_shows_six_seconds_plus_a_quarter_second_per_word_capped_at_twelve(int words, int expected) =>
        OverlayScheduler.DurationFor(string.Join(' ', Enumerable.Repeat("w", words))).ShouldBe(expected);

    [Fact]
    public void A_card_starts_two_seconds_after_its_moment()
    {
        var entry = Place([], Goal.Moment).ShouldHaveSingleItem();
        entry.Id.ShouldBe($"{Goal.Moment.Id}:lower_third");
        entry.InAtMs.ShouldBe(Goal.Moment.ClockMs + 2_000);
        entry.OutAtMs.ShouldBe(entry.InAtMs + 8_500);
        entry.State.ShouldBe(OverlayState.Scheduled);
        entry.Layer.ShouldBe(OverlayLayer.LowerThird);
        entry.Slot.ShouldBe(OverlaySlot.Bottom);
        entry.NarrativeId.ShouldBe($"{Goal.Moment.Id}:base:en");
    }

    [Fact]
    public void A_goal_carries_the_score_chips_with_values_from_code()
    {
        var entry = Place([], Goal.Moment).ShouldHaveSingleItem();
        entry.Chips.Select(c => c.FactKey).ShouldBe(["match.score.home", "match.score.away"]);
        entry.Chips.Select(c => c.Label).ShouldBe(["AMZ", "INY"]);
        entry.Chips.Select(c => c.Display).ShouldBe(["0", "1"]);
    }

    [Fact]
    public void A_momentum_swing_carries_momentum_and_shots_and_full_time_carries_nothing()
    {
        var swing = OverlayScheduler.Schedule([], Swing.Moment, Fixture.NarrativeFor(Swing.Moment, 5), Swing.Sheet, Fixture.Document, ApprovalMode.Auto).Single();
        swing.Chips.Select(c => c.Label).ShouldBe(["Momentum", "Shots"]);

        var fullTime = Fixture.First(MomentTypes.FullTime);
        var card = OverlayScheduler.Schedule([], fullTime.Moment, Fixture.NarrativeFor(fullTime.Moment, 5), fullTime.Sheet, Fixture.Document, ApprovalMode.Auto).Single();
        card.Chips.ShouldBeEmpty();
        card.FullWidth.ShouldBeTrue();
    }

    [Fact]
    public void An_equal_or_lower_priority_card_queues_behind_the_one_on_screen()
    {
        var first = Place([], Fixture.At(Swing.Moment, 100_000, 60)).Single();
        var second = Place([first], Fixture.At(Swing.Moment, 101_000, 50)).ShouldHaveSingleItem();

        second.InAtMs.ShouldBe(first.OutAtMs + OverlayScheduler.ExitMs);
        second.State.ShouldBe(OverlayState.Scheduled);
    }

    [Fact]
    public void A_higher_priority_card_pre_empts_the_one_on_screen()
    {
        var first = Place([], Fixture.At(Swing.Moment, 100_000, 60)).Single();
        var written = Place([first], Fixture.At(Goal.Moment, 103_000, 100));

        written.Count.ShouldBe(2);
        written[0].Id.ShouldBe(first.Id);
        written[0].OutAtMs.ShouldBe(105_000);
        written[1].InAtMs.ShouldBe(105_000 + OverlayScheduler.ExitMs);
    }

    [Fact]
    public void Pre_emption_pushes_queued_cards_back()
    {
        var first = Place([], Fixture.At(Swing.Moment, 100_000, 60)).Single();
        var queued = Place([first], Fixture.At(Swing.Moment, 101_000, 40)).Single();
        var written = Place([first, queued], Fixture.At(Goal.Moment, 103_000, 100));

        var goal = written.Single(e => e.Priority == 100);
        var moved = written.Single(e => e.Id == queued.Id);
        moved.InAtMs.ShouldBe(goal.OutAtMs + OverlayScheduler.ExitMs);
    }

    [Fact]
    public void A_card_that_would_start_more_than_45_seconds_late_expires()
    {
        var existing = new List<OverlayEntry>();
        for (var i = 0; i < 6; i++)
        {
            existing.AddRange(Place(existing, Fixture.At(Swing.Moment, 100_000 + i, 60), words: 30));
        }

        existing.Last().State.ShouldBe(OverlayState.Expired);
        existing.Take(4).ShouldAllBe(e => e.State == OverlayState.Scheduled);
    }

    [Fact]
    public void Approve_mode_holds_new_cards()
    {
        Place([], Goal.Moment, mode: ApprovalMode.Approve).Single().State.ShouldBe(OverlayState.Held);
    }

    [Fact]
    public void Held_and_expired_cards_do_not_block_the_line()
    {
        var held = Place([], Fixture.At(Swing.Moment, 100_000, 60), mode: ApprovalMode.Approve).Single();
        Place([held], Fixture.At(Swing.Moment, 101_000, 60)).Single().InAtMs.ShouldBe(103_000);
    }

    [Fact]
    public void Never_two_lower_thirds_on_screen_at_once_over_the_whole_sample()
    {
        var entries = new List<OverlayEntry>();
        foreach (var (moment, sheet) in Fixture.Moments)
        {
            foreach (var written in OverlayScheduler.Schedule(entries, moment, Fixture.NarrativeFor(moment, 20), sheet, Fixture.Document, ApprovalMode.Auto))
            {
                entries.RemoveAll(e => e.Id == written.Id);
                entries.Add(written);
            }
        }

        foreach (var period in new[] { 1, 2 })
        {
            var live = entries.Where(e => e.Period == period && e.State == OverlayState.Scheduled).OrderBy(e => e.InAtMs).ToList();
            for (var i = 1; i < live.Count; i++)
            {
                live[i].InAtMs.ShouldBeGreaterThanOrEqualTo(live[i - 1].OutAtMs, $"{live[i].Id} overlaps {live[i - 1].Id}");
            }
        }
    }

    [Fact]
    public void A_scheduled_card_reads_as_aired_once_its_out_time_has_passed()
    {
        var entry = Place([], Goal.Moment).Single();
        OverlayScheduler.Resolve(entry, entry.Period, entry.OutAtMs - 1).State.ShouldBe(OverlayState.Scheduled);
        OverlayScheduler.Resolve(entry, entry.Period, entry.OutAtMs).State.ShouldBe(OverlayState.Aired);
        OverlayScheduler.Resolve(entry, entry.Period + 1, 0).State.ShouldBe(OverlayState.Aired);
    }
}
