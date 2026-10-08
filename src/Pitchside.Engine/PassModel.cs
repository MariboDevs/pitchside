using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>Pass geometry and difficulty. Difficulty rises with length, forward progress and proximity to goal.</summary>
public static class PassModel
{
    public static Location SampleEnd(Pcg32 rng, Location from, bool forward)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var length = Math.Clamp(Math.Exp(rng.Gaussian(2.75, 0.55)), 3, 60);
        var angle = forward ? rng.Gaussian(0, 0.9) : rng.Gaussian(Math.PI, 1.2);
        var x = from.X + (length * Math.Cos(angle));
        var y = from.Y + (length * Math.Sin(angle));
        return new Location(
            Math.Round(Math.Clamp(x, 0.5, Pitch.LengthM - 0.5), 1),
            Math.Round(Math.Clamp(y, 0.5, Pitch.WidthM - 0.5), 1));
    }

    /// <summary>A rating in [0, 1]: the share of such passes a top-flight midfielder would be expected to miss.</summary>
    public static double Difficulty(Location from, Location to)
    {
        var length = from.DistanceTo(to);
        var progress = to.X - from.X;
        var difficulty = 0.06 + (0.0085 * length);
        if (progress > 12)
        {
            difficulty += 0.08;
        }

        if (to.X > 70)
        {
            difficulty += 0.10;
        }

        if (to.X > 88)
        {
            difficulty += 0.12;
        }

        return Math.Round(Math.Clamp(difficulty, 0.02, 0.95), 2);
    }
}
