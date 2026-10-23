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
    public void A_card_starts_two_seconds_after_its_narrative_is_ready_when_that_is_later()
    {
        var ready = Goal.Moment.ClockMs + 7_000;
        var entry = OverlayScheduler.Schedule([], Goal.Moment, Fixture.NarrativeFor(Goal.Moment, 10), Goal.Sheet, Fixture.Document, ApprovalMode.Auto, ready)
            .ShouldHaveSingleItem();
        entry.InAtMs.ShouldBe(ready + 2_000);
        entry.State.ShouldBe(OverlayState.Scheduled);
    }

    [Fact]
    public void A_card_due_during_the_previous_exit_waits_for_it()
    {
        var first = Place([], Goal.Moment).Single();
        var next = Goal.Moment with { Id = "MER-7:M:9001:goal", ClockMs = first.OutAtMs - OverlayScheduler.LeadMs + 50, Priority = Goal.Moment.Priority };

        var entry = Place([first], next).ShouldHaveSingleItem();

        entry.InAtMs.ShouldBe(first.OutAtMs + OverlayScheduler.ExitMs);
    }

    [Fact]
    public void A_held_card_does_not_touch_the_card_on_screen()
    {
        var onScreen = Place([], Swing.Moment with { Period = Goal.Moment.Period, ClockMs = Goal.Moment.ClockMs - 1_000 }).Single();

        var written = Place([onScreen], Goal.Moment, mode: ApprovalMode.Approve);

        written.ShouldHaveSingleItem().State.ShouldBe(OverlayState.Held);
    }

    [Fact]
    public void A_queued_card_expires_by_its_own_moment_however_often_it_is_moved()
    {
        var queued = Place([], Goal.Moment).Single() with { InAtMs = Goal.Moment.ClockMs + 40_000, OutAtMs = Goal.Moment.ClockMs + 48_000 };
        var deadlines = new Dictionary<string, int> { [queued.Id] = OverlayScheduler.DeadlineFor(Goal.Moment) };
        var urgent = Goal.Moment with { Id = "MER-7:M:9002:red_card", ClockMs = Goal.Moment.ClockMs + 37_000, Priority = 1_000 };
        var blocker = Place([], urgent with { Id = "MER-7:M:9003:goal", Priority = 1 }).Single() with { InAtMs = urgent.ClockMs + 1_000, OutAtMs = urgent.ClockMs + 41_000 };

        var written = OverlayScheduler.Schedule([blocker, queued], urgent, Fixture.NarrativeFor(urgent, 10), Goal.Sheet, Fixture.Document, ApprovalMode.Auto, null, deadlines);

        written.Single(e => e.Id == queued.Id).State.ShouldBe(OverlayState.Expired);
    }

    [Fact]
    public void A_card_whose_narrative_arrives_late_takes_the_free_gap_at_its_own_moment()
    {
        var later = Place([], Fixture.At(Swing.Moment, 200_000, 60)).Single();
        var earlier = Place([later], Fixture.At(Swing.Moment, 100_000, 60)).ShouldHaveSingleItem();

        earlier.InAtMs.ShouldBe(102_000);
        earlier.State.ShouldBe(OverlayState.Scheduled);
    }

    [Fact]
    public void A_card_that_does_not_fit_before_the_next_one_queues_behind_it()
    {
        var next = Place([], Fixture.At(Swing.Moment, 105_000, 60)).Single();
        var squeezed = Place([next], Fixture.At(Swing.Moment, 100_000, 60), words: 30).ShouldHaveSingleItem();

        squeezed.InAtMs.ShouldBe(next.OutAtMs + OverlayScheduler.ExitMs);
    }

    [Fact]
    public void An_overdue_card_leaves_the_card_on_screen_and_the_queue_alone()
    {
        var onScreen = Place([], Fixture.At(Swing.Moment, 100_000, 60)).Single();
        var late = onScreen with
        {
            Id = "MER-7:M:9004:red_card:lower_third",
            MomentId = "MER-7:M:9004:red_card",
            Priority = 1_000,
            InAtMs = onScreen.InAtMs + 1_000,
            OutAtMs = onScreen.InAtMs + 7_000,
        };

        var written = OverlayScheduler.Place([onScreen], late, late.InAtMs - 1, new Dictionary<string, int>());

        written.ShouldHaveSingleItem().State.ShouldBe(OverlayState.Expired);
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
            existing.AddRange(Place(existing, Fixture.At(Swing.Moment, 100_000 + (i * 1_000), 60), words: 30));
        }

        existing.Last().State.ShouldBe(OverlayState.Expired);
        existing.Take(4).ShouldAllBe(e => e.State == OverlayState.Scheduled);
    }

    [Fact]
    public void At_three_times_real_time_a_card_keeps_its_real_time_lead_airtime_and_deadline()
    {
        // At speed 3 the match clock runs three times as fast, so every match-clock span is three times as long and
        // the card reads on screen exactly as it does in real time.
        var ready = Goal.Moment.ClockMs + 7_000;
        var entry = OverlayScheduler.Schedule([], Goal.Moment, Fixture.NarrativeFor(Goal.Moment, 10), Goal.Sheet, Fixture.Document, ApprovalMode.Auto, ready, scale: 3)
            .ShouldHaveSingleItem();

        entry.InAtMs.ShouldBe(ready + 6_000);
        (entry.OutAtMs - entry.InAtMs).ShouldBe(25_500);
        OverlayScheduler.DeadlineFor(Goal.Moment, 3).ShouldBe(Goal.Moment.ClockMs + 135_000);
    }

    [Fact]
    public void At_the_largest_time_scale_a_late_card_still_fits_the_match_clock()
    {
        // A period's clock runs past 50 minutes with stoppage time; a deadline and a full-length card there must stay
        // positive ints.
        var late = Fixture.At(Goal.Moment, 3_000_000, Goal.Moment.Priority);
        OverlayScheduler.DeadlineFor(late, StoredMatch.MaxTimeScale).ShouldBeGreaterThan(late.ClockMs);
        var entry = OverlayScheduler.Schedule([], late, Fixture.NarrativeFor(late, 40), Goal.Sheet, Fixture.Document, ApprovalMode.Auto, scale: StoredMatch.MaxTimeScale)
            .ShouldHaveSingleItem();
        entry.OutAtMs.ShouldBeGreaterThan(entry.InAtMs);
        entry.State.ShouldBe(OverlayState.Scheduled);
    }

    [Fact]
    public void At_three_times_real_time_a_card_ready_a_minute_after_its_moment_still_airs()
    {
        // Twenty real seconds to write the card are sixty seconds of match time at speed 3: past the real-time
        // deadline, but inside the scaled one.
        var ready = Goal.Moment.ClockMs + 60_000;
        var narrative = Fixture.NarrativeFor(Goal.Moment, 10);
        OverlayScheduler.Schedule([], Goal.Moment, narrative, Goal.Sheet, Fixture.Document, ApprovalMode.Auto, ready)
            .ShouldHaveSingleItem().State.ShouldBe(OverlayState.Expired);
        OverlayScheduler.Schedule([], Goal.Moment, narrative, Goal.Sheet, Fixture.Document, ApprovalMode.Auto, ready, scale: 3)
            .ShouldHaveSingleItem().State.ShouldBe(OverlayState.Scheduled);
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
