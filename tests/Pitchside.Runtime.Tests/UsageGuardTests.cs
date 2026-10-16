using Microsoft.Extensions.Options;
using Pitchside.Runtime.Usage;

namespace Pitchside.Runtime.Tests;

public sealed class UsageGuardTests
{
    private static readonly DateTimeOffset TenPast = new(2026, 10, 25, 14, 10, 0, TimeSpan.Zero);

    private static UsageGuard Guard(ManualClock clock, bool enabled = true) =>
        new(Options.Create(new UsageGuardOptions { ReplaysPerHourPerIp = 2, NarrativesPerHour = 3, ConnectionsPerIp = 1 }), clock, enabled);

    [Fact]
    public void A_fixed_window_refuses_past_its_limit_until_the_next_hour()
    {
        var clock = new ManualClock(TenPast);
        var guard = Guard(clock);

        guard.TryTake(UsageLimits.Replays, "10.0.0.1").Allowed.ShouldBeTrue();
        guard.TryTake(UsageLimits.Replays, "10.0.0.1").Allowed.ShouldBeTrue();
        var refused = guard.TryTake(UsageLimits.Replays, "10.0.0.1");
        refused.Allowed.ShouldBeFalse();
        refused.RetryAfter.ShouldBe(TimeSpan.FromMinutes(50));
        guard.TryTake(UsageLimits.Replays, "10.0.0.2").Allowed.ShouldBeTrue();

        clock.Now = TenPast.AddMinutes(50);
        guard.TryTake(UsageLimits.Replays, "10.0.0.1").Allowed.ShouldBeTrue();
    }

    [Fact]
    public void Connections_are_counted_while_open()
    {
        var guard = Guard(new ManualClock(TenPast));

        guard.CanConnect("10.0.0.1").ShouldBeTrue();
        guard.TryConnect("10.0.0.1").ShouldBeTrue();
        guard.CanConnect("10.0.0.1").ShouldBeFalse();
        guard.TryConnect("10.0.0.1").ShouldBeFalse();
        guard.Disconnected("10.0.0.1");
        guard.CanConnect("10.0.0.1").ShouldBeTrue();
    }

    [Fact]
    public void A_disabled_guard_counts_but_never_refuses()
    {
        var guard = Guard(new ManualClock(TenPast), enabled: false);
        guard.TryConnect("10.0.0.1").ShouldBeTrue();

        for (var i = 0; i < 5; i++)
        {
            guard.TryTake(UsageLimits.Narratives, UsageGuard.HostKey).Allowed.ShouldBeTrue();
        }

        guard.CanConnect("10.0.0.1").ShouldBeTrue();
        var counts = guard.Snapshot("10.0.0.1");
        counts.Single(c => c.Name == UsageLimits.Narratives).ShouldBe(new UsageCount(UsageLimits.Narratives, "host per hour", 5, 3));
        counts.Single(c => c.Name == UsageLimits.Connections).Used.ShouldBe(1);
    }

    [Fact]
    public void The_snapshot_shows_every_limit_for_the_caller()
    {
        var guard = Guard(new ManualClock(TenPast));
        guard.TryTake(UsageLimits.Replays, "10.0.0.1");
        guard.TryTake(UsageLimits.Replays, "10.0.0.2");

        guard.Snapshot("10.0.0.1").Select(c => (c.Name, c.Used, c.Limit)).ShouldBe(
        [
            (UsageLimits.Replays, 1, 2),
            (UsageLimits.Narratives, 0, 3),
            (UsageLimits.SpokenLines, 0, 60),
            (UsageLimits.Connections, 0, 1),
        ]);
    }

    [Fact]
    public void Counts_from_an_earlier_hour_are_dropped_when_the_next_hour_starts()
    {
        var clock = new ManualClock(TenPast);
        var guard = Guard(clock);
        for (var i = 0; i < 50; i++)
        {
            guard.TryTake(UsageLimits.Replays, $"10.0.0.{i}");
        }

        guard.TrackedCounts.ShouldBe(50);
        clock.Now = TenPast.AddHours(1);
        guard.TryTake(UsageLimits.Replays, "10.0.1.1").Allowed.ShouldBeTrue();
        guard.TrackedCounts.ShouldBe(1);
    }

    [Fact]
    public void A_clock_that_steps_back_across_the_hour_never_reopens_a_full_window()
    {
        var clock = new ManualClock(TenPast.AddMinutes(50));
        var guard = Guard(clock);
        guard.TryTake(UsageLimits.Replays, "10.0.0.1").Allowed.ShouldBeTrue();
        guard.TryTake(UsageLimits.Replays, "10.0.0.1").Allowed.ShouldBeTrue();

        // A sample from just before the hour, applied after the new hour's quota is spent, counts in the new hour.
        clock.Now = TenPast.AddMinutes(49);
        guard.TryTake(UsageLimits.Replays, "10.0.0.1").Allowed.ShouldBeFalse();
        clock.Now = TenPast.AddMinutes(51);
        guard.TryTake(UsageLimits.Replays, "10.0.0.1").Allowed.ShouldBeFalse();
    }

    [Fact]
    public void A_use_given_back_comes_off_the_hour_it_was_taken_in()
    {
        var clock = new ManualClock(TenPast.AddMinutes(49));
        var guard = new UsageGuard(Options.Create(new UsageGuardOptions { ReplaysPerHourPerIp = 1 }), clock, true);

        // A reserves just before the hour; B starts just after it; then A's replay fails and gives its use back.
        var a = guard.TryTake(UsageLimits.Replays, "10.0.0.1");
        clock.Now = TenPast.AddMinutes(50);
        guard.TryTake(UsageLimits.Replays, "10.0.0.1").Allowed.ShouldBeTrue();
        guard.Release(UsageLimits.Replays, "10.0.0.1", a.Window);

        guard.TryTake(UsageLimits.Replays, "10.0.0.1").Allowed.ShouldBeFalse();
    }

    [Fact]
    public void Several_uses_are_taken_all_or_none()
    {
        var guard = new UsageGuard(Options.Create(new UsageGuardOptions { SpokenLinesPerHour = 4 }), new ManualClock(DateTimeOffset.UnixEpoch), enabled: true);

        guard.TryTake(UsageLimits.SpokenLines, UsageGuard.HostKey, 3).Allowed.ShouldBeTrue();
        guard.TryTake(UsageLimits.SpokenLines, UsageGuard.HostKey, 3).Allowed.ShouldBeFalse();
        guard.TryTake(UsageLimits.SpokenLines, UsageGuard.HostKey, 1).Allowed.ShouldBeTrue();
        guard.Snapshot("caller").Single(u => u.Name == UsageLimits.SpokenLines).Used.ShouldBe(4);
    }
}
