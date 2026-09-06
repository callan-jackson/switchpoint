using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Tests.Charges;

public class TieredChargeTests
{
    // AJ Bell Investcentre-style: 0.25% to £250k, 0.20% to £500k, 0.15% to £1m, 0.10% above (illustrative).
    private static readonly TieredCharge Marginal = TieredCharge.Marginal((250_000m, 0.0025m), (500_000m, 0.0020m), (1_000_000m, 0.0015m), (null, 0.0010m));
    private static readonly TieredCharge WholeOfFund = TieredCharge.WholeOfFund((250_000m, 0.0025m), (500_000m, 0.0020m), (null, 0.0015m));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100_000, 250)]
    [InlineData(250_000, 625)]            // exactly on the first boundary: all at 0.25%
    [InlineData(250_000.01, 625.00002)]   // one penny into band 2
    [InlineData(500_000, 1125)]           // 625 + 250k × 0.20%
    [InlineData(1_000_000, 1875)]         // 1125 + 500k × 0.15%
    [InlineData(2_000_000, 2875)]         // 1875 + 1m × 0.10%
    public void Marginal_charge_at_boundaries(decimal fundValue, decimal expected)
    {
        Assert.Equal(expected, Marginal.AnnualChargeFor(fundValue));
    }

    [Theory]
    [InlineData(100_000, 250)]
    [InlineData(250_000, 625)]     // on the boundary the lower band applies (UpTo is inclusive)
    [InlineData(250_000.01, 500.00002)]
    [InlineData(600_000, 900)]
    public void Whole_of_fund_charge_uses_containing_band(decimal fundValue, decimal expected)
    {
        Assert.Equal(expected, WholeOfFund.AnnualChargeFor(fundValue));
    }

    [Fact]
    public void Effective_rate_is_blended_for_marginal()
    {
        Assert.Equal(0.00225m, Marginal.EffectiveRateFor(500_000m));
        Assert.Equal(0.0025m, Marginal.EffectiveRateFor(0m));
    }

    [Fact]
    public void Flat_charge_is_a_single_unbounded_band()
    {
        TieredCharge flat = TieredCharge.Flat(0.0035m);
        Assert.Single(flat.Bands);
        Assert.Equal(350m, flat.AnnualChargeFor(100_000m));
    }

    [Fact]
    public void Rejects_bands_that_are_not_ascending() =>
        Assert.Throws<DomainException>(() => TieredCharge.Marginal((500_000m, 0.002m), (250_000m, 0.001m), (null, 0.001m)));

    [Fact]
    public void Rejects_bounded_last_band() =>
        Assert.Throws<DomainException>(() => TieredCharge.Marginal((250_000m, 0.002m), (500_000m, 0.001m)));

    [Fact]
    public void Rejects_unbounded_middle_band() =>
        Assert.Throws<DomainException>(() => TieredCharge.Marginal((null, 0.002m), (500_000m, 0.001m), (null, 0.001m)));

    [Fact]
    public void Rejects_empty_bands() =>
        Assert.Throws<ArgumentException>(() => new TieredCharge([], TieredChargeMode.Marginal));

    [Fact]
    public void Rejects_negative_or_over_100_percent_rates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierBand(null, -0.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierBand(null, 1.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TierBand(0m, 0.01m));
    }

    [Fact]
    public void Rejects_negative_fund_value() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Marginal.AnnualChargeFor(-1m));

    [Fact]
    public void Value_equality_is_structural()
    {
        TieredCharge a = TieredCharge.Marginal((100_000m, 0.003m), (null, 0.002m));
        TieredCharge b = TieredCharge.Marginal((100_000m, 0.003m), (null, 0.002m));
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, TieredCharge.WholeOfFund((100_000m, 0.003m), (null, 0.002m)));
    }

    private static Gen<TieredCharge> ArbitraryTiered(TieredChargeMode mode) =>
        from count in Gen.Choose(1, 5)
        from bounds in Gen.ArrayOf(Gen.Choose(1, 2_000_000), count - 1)
        from rates in Gen.ArrayOf(Gen.Choose(0, 200), count)
        let sorted = bounds.Distinct().OrderBy(b => b).Select(b => (decimal?)b).ToArray()
        let bands = sorted.Select((b, i) => (b, rates[i] / 10_000m)).Concat([((decimal?)null, rates[sorted.Length] / 10_000m)]).ToArray()
        select mode == TieredChargeMode.Marginal ? TieredCharge.Marginal(bands) : TieredCharge.WholeOfFund(bands);

    [Property(MaxTest = 300)]
    public Property Marginal_charge_is_monotonic_and_bounded()
    {
        Gen<decimal> values = Gen.Choose(0, 300_000_000).Select(v => v / 100m);
        return Prop.ForAll(ArbitraryTiered(TieredChargeMode.Marginal).ToArbitrary(), values.ToArbitrary(), values.ToArbitrary(), (t, a, b) =>
        {
            decimal lo = Math.Min(a, b);
            decimal hi = Math.Max(a, b);
            decimal cLo = t.AnnualChargeFor(lo);
            decimal cHi = t.AnnualChargeFor(hi);
            return cLo <= cHi && cHi <= t.MaxRate * hi && cHi >= t.MinRate * hi;
        });
    }

    [Property(MaxTest = 300)]
    public Property Whole_of_fund_charge_is_rate_of_containing_band_times_value()
    {
        Gen<decimal> values = Gen.Choose(0, 300_000_000).Select(v => v / 100m);
        return Prop.ForAll(ArbitraryTiered(TieredChargeMode.WholeOfFund).ToArbitrary(), values.ToArbitrary(), (t, v) =>
            t.AnnualChargeFor(v) == (v == 0m ? 0m : t.BandFor(v).AnnualRate * v));
    }

    [Property(MaxTest = 200)]
    public Property Whole_of_fund_is_never_dearer_than_marginal_when_rates_descend()
    {
        // With descending rates (the UK norm) whole-of-fund tiering charges the whole balance at the
        // lowest applicable rate, so it can never cost more than the marginal (slice) method.
        Gen<decimal> values = Gen.Choose(0, 300_000_000).Select(v => v / 100m);
        Gen<(decimal?, decimal)[]> bands = from b1 in Gen.Choose(10_000, 500_000)
                                           from b2 in Gen.Choose(500_001, 2_000_000)
                                           from r1 in Gen.Choose(20, 60)
                                           from r2 in Gen.Choose(10, 20)
                                           from r3 in Gen.Choose(0, 10)
                                           select new (decimal?, decimal)[] { (b1, r1 / 10_000m), (b2, r2 / 10_000m), (null, r3 / 10_000m) };
        return Prop.ForAll(bands.ToArbitrary(), values.ToArbitrary(), (b, v) =>
            TieredCharge.WholeOfFund(b).AnnualChargeFor(v) <= TieredCharge.Marginal(b).AnnualChargeFor(v) + 0.0000001m);
    }
}
