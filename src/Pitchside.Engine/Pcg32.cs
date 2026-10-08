namespace Pitchside.Engine;

/// <summary>
/// PCG-XSH-RR 32-bit generator. Owned rather than <see cref="Random"/> so a seed produces the same match on
/// every runtime version, which the golden-master tests depend on.
/// </summary>
public sealed class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;
    private ulong _state;
    private readonly ulong _increment;

    public Pcg32(ulong seed, ulong sequence = 54UL)
    {
        _increment = (sequence << 1) | 1UL;
        _state = 0;
        NextUInt32();
        _state += seed;
        NextUInt32();
    }

    public uint NextUInt32()
    {
        var old = _state;
        _state = (old * Multiplier) + _increment;
        var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        var rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }

    /// <summary>Uniform in [0, 1).</summary>
    public double NextDouble() => NextUInt32() * (1.0 / 4294967296.0);

    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    public int Next(int maxExclusive) => (int)(NextDouble() * maxExclusive);

    /// <summary>Uniform in [min, max).</summary>
    public double Range(double min, double max) => min + ((max - min) * NextDouble());

    public bool Chance(double probability) => NextDouble() < probability;

    /// <summary>Standard normal via Box-Muller.</summary>
    public double Gaussian(double mean, double standardDeviation)
    {
        var u1 = 1.0 - NextDouble();
        var u2 = NextDouble();
        var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + (standardDeviation * z);
    }

    /// <summary>Geometric number of trials with the given mean, at least one.</summary>
    public int GeometricAtLeastOne(double mean)
    {
        var p = 1.0 / Math.Max(1.0, mean);
        var n = 1;
        while (!Chance(p) && n < 40)
        {
            n++;
        }

        return n;
    }

    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items[Next(items.Count)];
    }
}
