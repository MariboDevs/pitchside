namespace Pitchside.Moments;

/// <summary>Moment type names and their priorities, from the WP-02 table.</summary>
public static class MomentTypes
{
    public const string Goal = "goal";
    public const string RedCard = "red_card";
    public const string Penalty = "penalty";
    public const string BigChanceMissed = "big_chance_missed";
    public const string MomentumSwing = "momentum_swing";
    public const string SustainedPressure = "sustained_pressure";
    public const string Milestone = "milestone";
    public const string SpeedTag = "speed_tag";
    public const string PassTag = "pass_tag";
    public const string KickOff = "kick_off";
    public const string HalfTime = "half_time";
    public const string FullTime = "full_time";

    public const int GoalPriority = 100;
    public const int RedCardPriority = 95;
    public const int PenaltyPriority = 90;
    public const int HalfTimePriority = 80;
    public const int BigChanceMissedPriority = 70;
    public const int MomentumSwingPriority = 60;
    public const int SustainedPressurePriority = 50;
    public const int MilestonePriority = 45;
    public const int SpeedTagPriority = 40;
    public const int PassTagPriority = 30;
    public const int KickOffPriority = 20;
    public const int FullTimePriority = 100;

    /// <summary>The types that are never rate-limited and never open a rate-limit gap.</summary>
    public static bool IsExempt(string type) => type is Goal or RedCard or Penalty or HalfTime or FullTime;
}
