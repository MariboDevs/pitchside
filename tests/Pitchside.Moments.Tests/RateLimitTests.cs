using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

public sealed class RateLimitTests
{
    private static Moment M(string type, int priority, int clockMs, string? side = Script.Home, int period = 1) => new()
    {
        Id = $"MER-1:M:{clockMs}:{type}",
        MatchId = "MER-1",
        Type = type,
        Priority = priority,
        Period = period,
        ClockMs = clockMs,
        TriggerSeq = 1,
        Window = new MomentWindow(1, 1),
        SideId = side,
        PlayerIds = [],
        Evidence = new MomentEvidence(["e"], []),
        Headline = "h",
        FactSheetSeq = 1,
    };

    [Fact]
    public void A_lower_or_equal_priority_moment_inside_ninety_seconds_is_blocked()
    {
        var limiter = MomentRateLimiter.Empty.Record(M(MomentTypes.MomentumSwing, 60, 0));
        limiter.BlockedForMatch(M(MomentTypes.SpeedTag, 40, 89_999)).ShouldBeTrue();
        limiter.BlockedForMatch(M(MomentTypes.MomentumSwing, 60, 30_000, Script.Away)).ShouldBeTrue();
        limiter.BlockedForMatch(M(MomentTypes.SpeedTag, 40, 90_000)).ShouldBeFalse();
        limiter.BlockedForMatch(M(MomentTypes.BigChanceMissed, 70, 30_000)).ShouldBeFalse();
    }

    [Fact]
    public void Exempt_types_are_never_blocked_and_never_open_a_gap()
    {
        var limiter = MomentRateLimiter.Empty.Record(M(MomentTypes.SpeedTag, 40, 0));
        foreach (var type in new[] { MomentTypes.Goal, MomentTypes.RedCard, MomentTypes.Penalty, MomentTypes.HalfTime, MomentTypes.FullTime })
        {
            limiter.BlockedForMatch(M(type, 1, 1_000)).ShouldBeFalse();
            MomentRateLimiter.Empty.Record(M(type, 100, 0)).BlockedForMatch(M(MomentTypes.PassTag, 30, 1_000)).ShouldBeFalse();
        }
    }

    [Fact]
    public void A_new_period_resets_the_gap()
    {
        MomentRateLimiter.Empty.Record(M(MomentTypes.SpeedTag, 40, 2_800_000))
            .BlockedForMatch(M(MomentTypes.SpeedTag, 40, 10_000, period: 2)).ShouldBeFalse();
    }

    [Fact]
    public void Momentum_swing_and_sustained_pressure_are_limited_per_side_for_five_minutes()
    {
        var limiter = MomentRateLimiter.Empty.Record(M(MomentTypes.SustainedPressure, 50, 0));
        limiter.BlockedForSide(M(MomentTypes.SustainedPressure, 50, 299_999)).ShouldBeTrue();
        limiter.BlockedForSide(M(MomentTypes.SustainedPressure, 50, 300_000)).ShouldBeFalse();
        limiter.BlockedForSide(M(MomentTypes.SustainedPressure, 50, 100_000, Script.Away)).ShouldBeFalse();
        limiter.BlockedForSide(M(MomentTypes.MomentumSwing, 60, 100_000)).ShouldBeFalse();
    }

    [Fact]
    public void The_detector_drops_a_lower_priority_moment_inside_the_gap()
    {
        // A momentum swing on the shot opens the gap; the hard pass that led to the same shot ranks lower.
        var moments = new Script().KickOff()
            .Possession().At(100).Pass(Script.Home, "HOM-07", 72, 88, difficulty: 0.65).Shot(Script.Home, "HOM-09", 0.05, ShotOutcome.OffTarget)
            .Moments();

        moments.Single(MomentTypes.MomentumSwing).TriggerSeq.ShouldBe(3);
        moments.ShouldHaveNone(MomentTypes.PassTag);
    }
}
