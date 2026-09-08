using SwitchPoint.Calculation.Numerics;

namespace SwitchPoint.Calculation.Tests.Numerics;

public class Xoshiro256StarStarTests
{
    [Fact]
    public void Same_seed_produces_identical_streams()
    {
        Xoshiro256StarStar a = new(42);
        Xoshiro256StarStar b = new(42);
        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }

        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(a.NextStandardNormal(), b.NextStandardNormal());
        }
    }

    [Fact]
    public void Different_seeds_diverge()
    {
        Xoshiro256StarStar a = new(1);
        Xoshiro256StarStar b = new(2);
        Assert.NotEqual(a.NextUInt64(), b.NextUInt64());
    }

    [Fact]
    public void Stream_is_pinned_so_platform_drift_is_detected()
    {
        // Golden values: xoshiro256** seeded through SplitMix64(0), computed from an independent
        // implementation of the published algorithm rather than from this one. They must never
        // change — a change would silently alter every stochastic result produced with a stored
        // seed, so a saved Monte Carlo run would stop reproducing.
        ulong[] expected = [0x99EC5F36CB75F2B4, 0xBF6E1F784956452A, 0x1A5F849D4933E6E0];
        Assert.Equal(expected, new Xoshiro256StarStar(0).Take(3));
    }

    [Fact]
    public void Doubles_are_in_unit_interval()
    {
        Xoshiro256StarStar rng = new(7);
        for (int i = 0; i < 100_000; i++)
        {
            double d = rng.NextDouble();
            Assert.InRange(d, 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void Open_double_is_never_zero()
    {
        Xoshiro256StarStar rng = new(11);
        for (int i = 0; i < 100_000; i++)
        {
            Assert.True(rng.NextOpenDouble() > 0.0);
        }
    }

    [Fact]
    public void Standard_normals_have_expected_moments()
    {
        Xoshiro256StarStar rng = new(2026);
        const int n = 200_000;
        double sum = 0;
        double sumSq = 0;
        for (int i = 0; i < n; i++)
        {
            double z = rng.NextStandardNormal();
            sum += z;
            sumSq += z * z;
        }

        double mean = sum / n;
        double variance = (sumSq / n) - (mean * mean);
        Assert.InRange(mean, -0.01, 0.01);
        Assert.InRange(variance, 0.98, 1.02);
    }

    [Fact]
    public void Decimal_normals_are_rounded_to_eighteen_places()
    {
        Xoshiro256StarStar rng = new(3);
        for (int i = 0; i < 1000; i++)
        {
            decimal z = rng.NextStandardNormalDecimal();
            Assert.Equal(z, Math.Round(z, 18));
            Assert.InRange(z, -8m, 8m);
        }
    }

    [Fact]
    public void Fill_matches_sequential_draws()
    {
        Xoshiro256StarStar a = new(99);
        Xoshiro256StarStar b = new(99);
        double[] buffer = new double[16];
        a.FillStandardNormals(buffer);
        foreach (double expected in buffer)
        {
            Assert.Equal(expected, b.NextStandardNormal());
        }
    }
}

internal static class XoshiroExtensions
{
    public static ulong[] Take(this Xoshiro256StarStar rng, int count)
    {
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = rng.NextUInt64();
        }

        return values;
    }
}
