using SwitchPoint.Calculation.Mortality;
using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Domain.Clients;

namespace SwitchPoint.Calculation.Annuities;

/// <summary>Terms of the annuity to price: £1 a year of income, monthly in advance.</summary>
/// <param name="Sex">Member's sex.</param>
/// <param name="Age">Member's age at commencement (may be fractional).</param>
/// <param name="InterestRate">Valuation interest rate (e.g. TVC annuity rate or COBS 13 Y-based rate).</param>
/// <param name="EscalationRate">Fixed annual increase in payment (0 for level; for index-linked use the rate the basis prescribes).</param>
/// <param name="GuaranteeYears">Guarantee period during which payments continue regardless of survival.</param>
/// <param name="SpouseFraction">Fraction of income continuing to a surviving spouse (0 for single life).</param>
/// <param name="SpouseAge">Spouse's age at commencement; null derives it from the age gap rule.</param>
/// <param name="SpouseSex">Spouse's sex; null = opposite sex.</param>
/// <param name="ExpenseLoading">Loading on the price (0.04m = 4%, COBS 13 Annex 2 3.1R / COBS 19 Annex 4C 1R(2)(g)).</param>
/// <param name="CalendarYear">Year of commencement for mortality improvements.</param>
/// <param name="SpouseAgeGapYears">Assumed age gap when <paramref name="SpouseAge"/> is not given (COBS 19 Annex 4C: 3 years).</param>
public sealed record AnnuityRequest(
    Sex Sex,
    decimal Age,
    decimal InterestRate,
    decimal EscalationRate = 0m,
    int GuaranteeYears = 0,
    decimal SpouseFraction = 0m,
    decimal? SpouseAge = null,
    Sex? SpouseSex = null,
    decimal ExpenseLoading = 0.04m,
    int CalendarYear = 2026,
    int SpouseAgeGapYears = 3);

/// <summary>Result of pricing £1 a year of income.</summary>
/// <param name="Factor">Present value of £1 a year (before expenses).</param>
/// <param name="PricePerPound">Factor × (1 + expense loading): the capital needed per £1 of annual income.</param>
/// <param name="AnnuityRate">Income per £1 of capital = 1 / PricePerPound.</param>
/// <param name="MemberExpectancy">Member's expectation of life at commencement, from the table.</param>
/// <param name="MortalityBasis">Name of the life table used.</param>
public sealed record AnnuityFactorResult(decimal Factor, decimal PricePerPound, decimal AnnuityRate, decimal MemberExpectancy, string MortalityBasis);

/// <summary>
/// Prices pension annuities from a life table and an interest rate: level or escalating, single or joint life,
/// with a guarantee period, monthly in advance. See docs/methodology/annuity.md.
/// </summary>
public sealed class AnnuityPricer
{
    private const int MaxYears = 121;
    private readonly ILifeTable _lifeTable;

    public AnnuityPricer(ILifeTable lifeTable)
    {
        _lifeTable = lifeTable ?? throw new ArgumentNullException(nameof(lifeTable));
    }

    public AnnuityFactorResult Factor(AnnuityRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.Age < 0m || r.Age > 110m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), r.Age, "Age must be between 0 and 110.");
        }

        if (r.InterestRate <= -1m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), r.InterestRate, "Interest rate must exceed -100%.");
        }

        if (r.SpouseFraction < 0m || r.SpouseFraction > 1m || r.GuaranteeYears < 0 || r.GuaranteeYears > 30 || r.ExpenseLoading < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), "Spouse fraction must be 0–1, guarantee 0–30 years, loading non-negative.");
        }

        Sex spouseSex = r.SpouseSex ?? (r.Sex == Sex.Male ? Sex.Female : Sex.Male);
        // COBS 19 Annex 4C 1R(2)(h): male member → female spouse 3 years younger; female member → male spouse 3 years older.
        decimal spouseAge = r.SpouseAge ?? (r.Sex == Sex.Male ? r.Age - r.SpouseAgeGapYears : r.Age + r.SpouseAgeGapYears);
        spouseAge = Math.Max(0m, spouseAge);

        decimal vMonthly = 1m / DecimalMath.NthRoot(1m + r.InterestRate, 12);
        decimal discount = 1m;
        decimal factor = 0m;
        int guaranteeMonths = r.GuaranteeYears * 12;

        for (int k = 0; k < MaxYears * 12; k++)
        {
            decimal t = k / 12m;
            decimal payment = DecimalMath.IntegerPow(1m + r.EscalationRate, k / 12) / 12m;
            decimal probability;
            if (k < guaranteeMonths)
            {
                probability = 1m;
            }
            else
            {
                decimal pMember = _lifeTable.SurvivalProbability(r.Sex, r.Age, t, r.CalendarYear);
                probability = pMember;
                if (r.SpouseFraction > 0m)
                {
                    decimal pSpouse = _lifeTable.SurvivalProbability(spouseSex, spouseAge, t, r.CalendarYear);
                    probability += r.SpouseFraction * (1m - pMember) * pSpouse;
                }

                if (probability < 1e-15m)
                {
                    break;
                }
            }

            factor += discount * payment * probability;
            discount *= vMonthly;
        }

        decimal price = factor * (1m + r.ExpenseLoading);
        return new AnnuityFactorResult(factor, price, price == 0m ? 0m : 1m / price, _lifeTable.LifeExpectancy(r.Sex, r.Age, r.CalendarYear), _lifeTable.Name);
    }

    /// <summary>Capital needed to buy <paramref name="annualIncome"/> a year on the given terms.</summary>
    public decimal Cost(decimal annualIncome, AnnuityRequest request)
    {
        if (annualIncome < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(annualIncome), annualIncome, "Income cannot be negative.");
        }

        return annualIncome * Factor(request).PricePerPound;
    }
}
