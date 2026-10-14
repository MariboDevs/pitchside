namespace Pitchside.Stats;

/// <summary>The tag names the stats layer raises, and the thresholds that raise them.</summary>
public static class TagNames
{
    public const string Speed30 = "speed.30";
    public const string Speed33 = "speed.33";
    public const string Distance5 = "distance.5";
    public const string Distance10 = "distance.10";
    public const string ShotFast = "shot.fast";
    public const string PassHard = "pass.hard";

    public const double FastShotKph = 100;
    public const double HardPassDifficulty = 0.6;
    public const double SprintCarryKph = 25;
    public const double SprintTrackingKph = 30;
}
