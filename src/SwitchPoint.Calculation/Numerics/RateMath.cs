namespace SwitchPoint.Calculation.Numerics;

/// <summary>
/// Conversions between annual and periodic rates and between nominal and real terms. Rates are
/// decimal fractions (0.05m is five per cent). See docs/methodology/numerics.md.
/// </summary>
public static class RateMath
{
    /// <summary>Geometric conversion of an effective annual rate to the equivalent monthly rate: (1+r)^(1/12) − 1.</summary>
    public static decimal AnnualToMonthly(decimal annualRate)
    {
        if (annualRate <= -1m)
        {
            throw new ArgumentOutOfRangeException(nameof(annualRate), annualRate, "An annual rate must be greater than −100%.");
        }

        return DecimalMath.NthRoot(1m + annualRate, 12) - 1m;
    }

    /// <summary>Geometric conversion of an effective annual rate to the equivalent rate for <paramref name="periodsPerYear"/> periods.</summary>
    public static decimal AnnualToPeriodic(decimal annualRate, int periodsPerYear)
    {
        if (periodsPerYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(periodsPerYear), periodsPerYear, "There must be at least one period per year.");
        }

        if (annualRate <= -1m)
        {
            throw new ArgumentOutOfRangeException(nameof(annualRate), annualRate, "An annual rate must be greater than −100%.");
        }

        return DecimalMath.NthRoot(1m + annualRate, periodsPerYear) - 1m;
    }

    /// <summary>Compounds a monthly rate back to an effective annual rate: (1+m)^12 − 1.</summary>
    public static decimal MonthlyToAnnual(decimal monthlyRate) => DecimalMath.IntegerPow(1m + monthlyRate, 12) - 1m;

    /// <summary>Fisher relation: the real rate implied by a nominal rate and inflation, (1+n)/(1+i) − 1.</summary>
    public static decimal Real(decimal nominalRate, decimal inflation)
    {
        if (inflation <= -1m)
        {
            throw new ArgumentOutOfRangeException(nameof(inflation), inflation, "Inflation must be greater than −100%.");
        }

        return ((1m + nominalRate) / (1m + inflation)) - 1m;
    }

    /// <summary>Fisher relation: the nominal rate implied by a real rate and inflation, (1+r)(1+i) − 1.</summary>
    public static decimal Nominal(decimal realRate, decimal inflation) => ((1m + realRate) * (1m + inflation)) - 1m;

    /// <summary>Growth factor over a fractional number of years at an effective annual rate: (1+r)^t.</summary>
    public static decimal GrowthFactor(decimal annualRate, decimal years)
    {
        if (annualRate <= -1m)
        {
            throw new ArgumentOutOfRangeException(nameof(annualRate), annualRate, "An annual rate must be greater than −100%.");
        }

        return DecimalMath.Pow(1m + annualRate, years);
    }

    /// <summary>Discount factor over a fractional number of years: (1+r)^−t.</summary>
    public static decimal DiscountFactor(decimal annualRate, decimal years) => 1m / GrowthFactor(annualRate, years);

    /// <summary>
    /// Rounds <paramref name="value"/> down (towards zero) to <paramref name="significantFigures"/>
    /// significant figures, as COBS 13 Annex 2 1.2R requires for standardised projection values.
    /// </summary>
    public static decimal RoundDownToSignificant(decimal value, int significantFigures)
    {
        if (significantFigures < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(significantFigures), significantFigures, "At least one significant figure is required.");
        }

        if (value == 0m)
        {
            return 0m;
        }

        decimal magnitude = Math.Abs(value);
        int exponent = 0;
        while (magnitude >= 10m)
        {
            magnitude /= 10m;
            exponent++;
        }

        while (magnitude < 1m)
        {
            magnitude *= 10m;
            exponent--;
        }

        // Number of decimal places to keep so that we retain the requested significant figures.
        int decimals = significantFigures - 1 - exponent;
        decimal scale = DecimalMath.IntegerPow(10m, Math.Abs(decimals));
        decimal scaled = decimals >= 0 ? Math.Abs(value) * scale : Math.Abs(value) / scale;
        decimal truncated = decimal.Truncate(scaled);
        decimal result = decimals >= 0 ? truncated / scale : truncated * scale;
        return value < 0m ? -result : result;
    }

    /// <summary>Rounds to the nearest multiple of <paramref name="step"/> (e.g. 0.001m for 0.1%), midpoints away from zero.</summary>
    public static decimal RoundToNearest(decimal value, decimal step)
    {
        if (step <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "The rounding step must be positive.");
        }

        return Math.Round(value / step, 0, MidpointRounding.AwayFromZero) * step;
    }

    /// <summary>Rounds money to pence using banker's rounding (the default for presentation).</summary>
    public static decimal ToPence(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);
}
