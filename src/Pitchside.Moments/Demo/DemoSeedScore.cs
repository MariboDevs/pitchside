namespace Pitchside.Moments.Demo;

/// <summary>
/// How well a seed would demo (WP-11): one point for each of the five things a judge should see in one match. The
/// highest total wins, and the lowest seed breaks a tie so the choice is the same on every machine.
/// </summary>
public sealed record DemoSeedScore(
    long Seed,
    int Goals,
    bool GoalsInRange,
    bool Comeback,
    bool RedCardOrPenalty,
    bool BigChanceMissed,
    bool MomentumSwingEachHalf)
{
    public int Total =>
        (GoalsInRange ? 1 : 0) + (Comeback ? 1 : 0) + (RedCardOrPenalty ? 1 : 0) + (BigChanceMissed ? 1 : 0) + (MomentumSwingEachHalf ? 1 : 0);
}
