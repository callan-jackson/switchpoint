using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using SwitchPoint.Calculation.Numerics;

namespace SwitchPoint.Calculation.Tests.Numerics;

public class RateMathTests
{
    [Fact]
    public void Monthly_rate_compounds_to_annual()
    {
        decimal m = RateMath.AnnualToMonthly(0.05m);
        Assert.InRange(Math.Abs(RateMath.MonthlyToAnnual(m) - 0.05m), 0m, 1e-24m);
        Assert.InRange(m, 0.00407m, 0.00408m); // 0.4074% a month
    }

    [Fact]
    public void Zero_annual_rate_is_zero_monthly() => Assert.Equal(0m, RateMath.AnnualToMonthly(0m));

    [Fact]
    public void Negative_growth_converts_symmetrically()
    {
        decimal m = RateMath.AnnualToMonthly(-0.10m);
        Assert.True(m < 0m);
        Assert.InRange(Math.Abs(RateMath.MonthlyToAnnual(m) + 0.10m), 0m, 1e-24m);
    }

    [Fact]
    public void Annual_rate_at_or_below_minus_100_percent_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RateMath.AnnualToMonthly(-1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => RateMath.AnnualToMonthly(-1.5m));
    }

    [Fact]
    public void Quarterly_conversion_matches_fourth_root()
    {
        decimal q = RateMath.AnnualToPeriodic(0.08m, 4);
        Assert.InRange(Math.Abs(DecimalMath.IntegerPow(1m + q, 4) - 1.08m), 0m, 1e-24m);
    }

    [Fact]
    public void Fisher_relation_round_trips()
    {
        decimal real = RateMath.Real(0.05m, 0.02m);
        Assert.InRange(Math.Abs(real - 0.0294117647058823529411764706m), 0m, 1e-24m);
        Assert.InRange(Math.Abs(RateMath.Nominal(real, 0.02m) - 0.05m), 0m, 1e-24m);
    }

    [Fact]
    public void Growth_and_discount_factors_are_reciprocal()
    {
        decimal g = RateMath.GrowthFactor(0.04m, 7.5m);
        decimal d = RateMath.DiscountFactor(0.04m, 7.5m);
        Assert.InRange(Math.Abs((g * d) - 1m), 0m, 1e-24m);
        Assert.Equal(1.1664m, RateMath.GrowthFactor(0.08m, 2m));
    }

    [Theory]
    [InlineData("123456.789", 3, "123000")]
    [InlineData("123999.99", 3, "123000")]
    [InlineData("0.0123456", 3, "0.0123")]
    [InlineData("9.999", 2, "9.9")]
    [InlineData("-123456", 3, "-123000")]
    [InlineData("0", 3, "0")]
    [InlineData("1", 3, "1")]
    [InlineData("987654321", 1, "900000000")]
    public void Rounding_down_to_significant_figures(string value, int figures, string expected)
    {
        decimal v = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        decimal e = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(e, RateMath.RoundDownToSignificant(v, figures));
    }

    [Theory]
    [InlineData("0.01234", "0.001", "0.012")]
    [InlineData("0.01250", "0.001", "0.013")]
    [InlineData("0.01249", "0.001", "0.012")]
    [InlineData("0.0331", "0.002", "0.034")]
    [InlineData("-0.0125", "0.001", "-0.013")]
    public void Rounding_to_nearest_step(string value, string step, string expected)
    {
        decimal v = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        decimal s = decimal.Parse(step, System.Globalization.CultureInfo.InvariantCulture);
        decimal e = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(e, RateMath.RoundToNearest(v, s));
    }

    [Fact]
    public void Pence_rounding_is_bankers()
    {
        Assert.Equal(1.12m, RateMath.ToPence(1.125m));
        Assert.Equal(1.14m, RateMath.ToPence(1.135m));
        Assert.Equal(1.13m, RateMath.ToPence(1.1251m));
    }

    [Property(MaxTest = 300)]
    public Property Rounding_down_never_increases_magnitude_and_keeps_sign()
    {
        Gen<decimal> gen = Gen.Choose(-2_000_000_000, 2_000_000_000).Select(i => i / 1000m);
        Gen<int> figures = Gen.Choose(1, 6);
        return Prop.ForAll(gen.ToArbitrary(), figures.ToArbitrary(), (v, f) =>
        {
            decimal r = RateMath.RoundDownToSignificant(v, f);
            return Math.Abs(r) <= Math.Abs(v) && Math.Sign(r) * Math.Sign(v) >= 0;
        });
    }

    [Property(MaxTest = 300)]
    public Property Monthly_conversion_is_monotonic()
    {
        Gen<decimal> gen = Gen.Choose(-900, 500).Select(i => i / 1000m);
        return Prop.ForAll(gen.ToArbitrary(), gen.ToArbitrary(), (a, b) =>
            a == b || (a < b) == (RateMath.AnnualToMonthly(a) < RateMath.AnnualToMonthly(b)));
    }
}
