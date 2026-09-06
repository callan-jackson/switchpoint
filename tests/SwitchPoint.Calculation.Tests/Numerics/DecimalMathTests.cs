using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using SwitchPoint.Calculation.Numerics;

namespace SwitchPoint.Calculation.Tests.Numerics;

public class DecimalMathTests
{
    private const decimal Tight = 1e-24m;

    [Fact]
    public void Exp_of_zero_is_one() => Assert.Equal(1m, DecimalMath.Exp(0m));

    [Fact]
    public void Exp_of_one_is_e() => Assert.InRange(Math.Abs(DecimalMath.Exp(1m) - DecimalMath.E), 0m, Tight);

    [Theory]
    [InlineData("0.5", "1.6487212707001281468486507878")]
    [InlineData("-1", "0.3678794411714423215955237702")]
    [InlineData("10", "22026.465794806716516957900645")]
    [InlineData("-20", "0.0000000020611536224385578280")]
    public void Exp_matches_reference_values(string x, string expected)
    {
        decimal actual = DecimalMath.Exp(decimal.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
        decimal want = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(Math.Abs(actual - want) / want, 0m, 1e-22m);
    }

    [Fact]
    public void Exp_far_below_range_returns_zero() => Assert.Equal(0m, DecimalMath.Exp(-100m));

    [Fact]
    public void Exp_overflows_beyond_decimal_range() => Assert.Throws<OverflowException>(() => DecimalMath.Exp(70m));

    [Fact]
    public void Ln_of_one_is_zero() => Assert.Equal(0m, DecimalMath.Ln(1m));

    [Fact]
    public void Ln_of_e_is_one() => Assert.InRange(Math.Abs(DecimalMath.Ln(DecimalMath.E) - 1m), 0m, Tight);

    [Theory]
    [InlineData("2", "0.6931471805599453094172321215")]
    [InlineData("10", "2.3025850929940456840179914547")]
    [InlineData("0.001", "-6.9077552789821370520539743640")]
    [InlineData("1.05", "0.0487901641694320030653744042")]
    public void Ln_matches_reference_values(string x, string expected)
    {
        decimal actual = DecimalMath.Ln(decimal.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
        decimal want = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(Math.Abs(actual - want), 0m, 1e-23m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ln_rejects_non_positive(decimal x) => Assert.Throws<ArgumentOutOfRangeException>(() => DecimalMath.Ln(x));

    [Fact]
    public void Pow_integer_exponent_is_exact()
    {
        Assert.Equal(1.1025m, DecimalMath.Pow(1.05m, 2m));
        Assert.Equal(1024m, DecimalMath.Pow(2m, 10m));
        Assert.Equal(0.25m, DecimalMath.Pow(2m, -2m));
        Assert.Equal(-8m, DecimalMath.Pow(-2m, 3m));
    }

    [Fact]
    public void Pow_zero_base_rules()
    {
        Assert.Equal(1m, DecimalMath.Pow(0m, 0m));
        Assert.Equal(0m, DecimalMath.Pow(0m, 2.5m));
        Assert.Throws<ArgumentOutOfRangeException>(() => DecimalMath.Pow(0m, -1m));
    }

    [Fact]
    public void Pow_negative_base_requires_integer_exponent() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DecimalMath.Pow(-2m, 0.5m));

    [Fact]
    public void Twelfth_root_of_growth_compounds_back_exactly()
    {
        decimal monthly = DecimalMath.Pow(1.05m, 1m / 12m);
        decimal annual = DecimalMath.IntegerPow(monthly, 12);
        Assert.InRange(Math.Abs(annual - 1.05m), 0m, 1e-24m);
    }

    [Fact]
    public void Sqrt_of_perfect_squares_is_exact()
    {
        Assert.Equal(12m, DecimalMath.Sqrt(144m));
        Assert.Equal(0.5m, DecimalMath.Sqrt(0.25m));
        Assert.Equal(1000000m, DecimalMath.Sqrt(1_000_000_000_000m));
    }

    [Fact]
    public void Sqrt_two_squared_is_two() => Assert.InRange(Math.Abs((DecimalMath.Sqrt(2m) * DecimalMath.Sqrt(2m)) - 2m), 0m, 1e-26m);

    [Fact]
    public void Sqrt_rejects_negative() => Assert.Throws<ArgumentOutOfRangeException>(() => DecimalMath.Sqrt(-1m));

    [Fact]
    public void NthRoot_round_trips()
    {
        decimal r = DecimalMath.NthRoot(1.07m, 12);
        Assert.InRange(Math.Abs(DecimalMath.IntegerPow(r, 12) - 1.07m), 0m, 1e-26m);
        Assert.Equal(3m, DecimalMath.NthRoot(27m, 3));
        Assert.Equal(5m, DecimalMath.NthRoot(5m, 1));
    }

    [Property(MaxTest = 200)]
    public Property Ln_inverts_Exp()
    {
        Gen<decimal> gen = Gen.Choose(-20_000, 20_000).Select(i => i / 1000m);
        return Prop.ForAll(gen.ToArbitrary(), x =>
        {
            decimal roundTrip = DecimalMath.Ln(DecimalMath.Exp(x));
            return Math.Abs(roundTrip - x) < 1e-18m;
        });
    }

    [Property(MaxTest = 200)]
    public Property Exp_inverts_Ln()
    {
        Gen<decimal> gen = Gen.Choose(1, 100_000_000).Select(i => i / 1000m);
        return Prop.ForAll(gen.ToArbitrary(), x =>
        {
            decimal roundTrip = DecimalMath.Exp(DecimalMath.Ln(x));
            return Math.Abs(roundTrip - x) / x < 1e-22m;
        });
    }

    [Property(MaxTest = 200)]
    public Property Exp_is_strictly_increasing()
    {
        Gen<decimal> gen = Gen.Choose(-30_000, 30_000).Select(i => i / 1000m);
        return Prop.ForAll(gen.ToArbitrary(), gen.ToArbitrary(), (a, b) => a == b || (a < b) == (DecimalMath.Exp(a) < DecimalMath.Exp(b)));
    }

    [Property(MaxTest = 200)]
    public Property Sqrt_squared_returns_input()
    {
        Gen<decimal> gen = Gen.Choose(0, 1_000_000_000).Select(i => i / 100m);
        return Prop.ForAll(gen.ToArbitrary(), x =>
        {
            decimal s = DecimalMath.Sqrt(x);
            return Math.Abs((s * s) - x) <= Math.Max(1e-24m, x * 1e-26m);
        });
    }
}
