namespace SwitchPoint.Calculation.Numerics;

/// <summary>
/// xoshiro256** pseudo-random generator (Blackman &amp; Vigna, 2018). Seeded deterministically via
/// SplitMix64 so that the same seed yields the same stream on every platform and .NET version —
/// unlike <see cref="Random"/>, whose seeded algorithm is not a compatibility guarantee.
/// This is the only place in the calculation engine where <see cref="double"/> arithmetic is used;
/// callers convert to <see cref="decimal"/> before touching money. See docs/methodology/monte-carlo.md.
/// </summary>
public sealed class Xoshiro256StarStar
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;
    private bool _hasSpareNormal;
    private double _spareNormal;

    /// <summary>Creates a generator whose state is derived from <paramref name="seed"/> with SplitMix64.</summary>
    public Xoshiro256StarStar(ulong seed)
    {
        Seed = seed;
        ulong x = seed;
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
        if ((_s0 | _s1 | _s2 | _s3) == 0)
        {
            _s0 = 0x9E3779B97F4A7C15UL; // the all-zero state is the one forbidden state
        }
    }

    /// <summary>The seed this generator was constructed with (recorded on results for reproducibility).</summary>
    public ulong Seed { get; }

    /// <summary>Next 64 random bits.</summary>
    public ulong NextUInt64()
    {
        ulong result = RotateLeft(_s1 * 5UL, 7) * 9UL;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }

    /// <summary>Uniform double in [0, 1) with 53 bits of precision.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Uniform double in (0, 1), never exactly zero (safe for logarithms).</summary>
    public double NextOpenDouble()
    {
        double u;
        do
        {
            u = NextDouble();
        }
        while (u == 0.0);
        return u;
    }

    /// <summary>Standard normal variate via the polar Box–Muller method.</summary>
    public double NextStandardNormal()
    {
        if (_hasSpareNormal)
        {
            _hasSpareNormal = false;
            return _spareNormal;
        }

        double u;
        double v;
        double s;
        do
        {
            u = (2.0 * NextDouble()) - 1.0;
            v = (2.0 * NextDouble()) - 1.0;
            s = (u * u) + (v * v);
        }
        while (s >= 1.0 || s == 0.0);

        double multiplier = Math.Sqrt(-2.0 * Math.Log(s) / s);
        _spareNormal = v * multiplier;
        _hasSpareNormal = true;
        return u * multiplier;
    }

    /// <summary>Standard normal variate converted to decimal (rounded to 18 places; the boundary where double leaves the engine).</summary>
    public decimal NextStandardNormalDecimal() => Math.Round((decimal)NextStandardNormal(), 18, MidpointRounding.ToEven);

    /// <summary>Fills <paramref name="buffer"/> with independent standard normals.</summary>
    public void FillStandardNormals(Span<double> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = NextStandardNormal();
        }
    }

    private static ulong SplitMix64(ref ulong state)
    {
        ulong z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));
}
