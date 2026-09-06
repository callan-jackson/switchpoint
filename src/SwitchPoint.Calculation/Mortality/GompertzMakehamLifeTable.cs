using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Domain.Clients;

namespace SwitchPoint.Calculation.Mortality;

/// <summary>
/// Gompertz–Makeham mortality μ(x) = A + B·c^x, with parameters chosen so that period life expectancy at 65
/// matches ONS National Life Tables 2020–22 (male 18.3 years, female 20.8 years) and the shape matches UK
/// pensioner mortality. A CMI-style improvement of 1.25% a year is applied from the base year (2021), so a
/// life aged x in calendar year Y faces μ(x) × (1 − 0.0125)^(Y − 2021).
/// This is a documented approximation to the PMA16/PFA16 + CMI basis prescribed by COBS 13 Annex 2 3.1R and
/// COBS 19 Annex 4C, which are licensed tables; see docs/methodology/annuity.md.
/// </summary>
public sealed class GompertzMakehamLifeTable : ILifeTable
{
    private const int BaseYear = 2021;
    private const decimal Improvement = 0.0125m;
    private const decimal MaxAge = 120m;

    // Parameters (A, B, c) per sex. Calibrated numerically: e65 = 18.3 (M) / 20.8 (F) on the base year.
    private static readonly (decimal A, decimal B, decimal C) Male = (0.0002m, 0.000027394m, 1.1000m);
    private static readonly (decimal A, decimal B, decimal C) Female = (0.0002m, 0.000016141m, 1.1030m);

    public static GompertzMakehamLifeTable Default { get; } = new();

    public string Name => "Gompertz–Makeham fit to ONS National Life Tables 2020–22 with 1.25% p.a. improvements (approximation to PMA16/PFA16 + CMI)";

    public decimal SurvivalProbability(Sex sex, decimal age, decimal years, int calendarYear)
    {
        if (age < 0m || years < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(years), "Age and duration must be non-negative.");
        }

        if (years == 0m)
        {
            return 1m;
        }

        if (age + years >= MaxAge)
        {
            return 0m;
        }

        (decimal a, decimal b, decimal c) = sex == Sex.Male ? Male : Female;
        decimal improvement = DecimalMath.Pow(1m - Improvement, Math.Max(0, calendarYear - BaseYear));
        // ∫_x^{x+t} (A + B c^s) ds = A t + B (c^{x+t} − c^x) / ln c
        decimal lnC = DecimalMath.Ln(c);
        decimal integral = (a * years) + (b * (DecimalMath.Pow(c, age + years) - DecimalMath.Pow(c, age)) / lnC);
        return DecimalMath.Exp(-integral * improvement);
    }

    public decimal LifeExpectancy(Sex sex, decimal age, int calendarYear)
    {
        // Trapezoidal integration of the survival curve in monthly steps to age 120.
        decimal sum = 0m;
        const decimal step = 1m / 12m;
        decimal previous = 1m;
        for (decimal t = step; age + t <= MaxAge; t += step)
        {
            decimal p = SurvivalProbability(sex, age, t, calendarYear);
            sum += (previous + p) / 2m * step;
            previous = p;
            if (p < 1e-12m)
            {
                break;
            }
        }

        return sum;
    }
}
