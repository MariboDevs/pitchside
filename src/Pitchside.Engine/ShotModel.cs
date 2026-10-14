using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>
/// Shot quality and outcome. xG is drawn from a three-part mixture (half-chances, decent chances, big chances)
/// whose mean reproduces the real goals-per-shot ratio; a goal is then a Bernoulli draw on that xG, so the
/// synthetic goals-per-shot and goals-per-shot-on-target match the targets by construction.
/// </summary>
public static class ShotModel
{
    /// <summary>Probability a non-scoring shot is on target, chosen so overall on-target share lands at ~34%.</summary>
    private const double OnTargetWhenNotGoal = 0.258;

    /// <summary>The penalty spot in the attacking frame: eleven metres out, central.</summary>
    public static readonly Location PenaltySpot = new(94, 34);

    /// <summary>The fixed xG of a penalty kick.</summary>
    public const double PenaltyXg = 0.76;

    public static double SampleXg(Pcg32 rng, out Location location)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var component = rng.NextDouble();
        if (component < 0.62)
        {
            // Half-chance: long range or tight angle.
            location = new Location(rng.Range(80, 94), rng.Range(10, 58));
            return rng.Range(0.01, 0.07);
        }

        if (component < 0.89)
        {
            // Decent chance: edge of the box or a header.
            location = new Location(rng.Range(88, 99), rng.Range(18, 50));
            return rng.Range(0.07, 0.20);
        }

        // Big chance: central, close.
        location = new Location(rng.Range(96, 104), rng.Range(26, 42));
        return rng.Range(0.20, 0.62);
    }

    public static ShotOutcome ResolveOutcome(Pcg32 rng, double xg)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (rng.Chance(xg))
        {
            return ShotOutcome.Goal;
        }

        if (rng.Chance(OnTargetWhenNotGoal))
        {
            return ShotOutcome.Saved;
        }

        var miss = rng.NextDouble();
        if (miss < 0.37)
        {
            return ShotOutcome.Blocked;
        }

        return miss < 0.95 ? ShotOutcome.OffTarget : ShotOutcome.Post;
    }

    public static BodyPart SampleBodyPart(Pcg32 rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var u = rng.NextDouble();
        if (u < 0.15)
        {
            return BodyPart.Head;
        }

        if (u < 0.75)
        {
            return BodyPart.RightFoot;
        }

        return u < 0.95 ? BodyPart.LeftFoot : BodyPart.Other;
    }

    public static double SampleShotSpeedKph(Pcg32 rng, BodyPart bodyPart)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var speed = bodyPart == BodyPart.Head ? rng.Gaussian(55, 10) : rng.Gaussian(86, 14);
        return Math.Round(Math.Clamp(speed, 35, 135), 1);
    }
}
