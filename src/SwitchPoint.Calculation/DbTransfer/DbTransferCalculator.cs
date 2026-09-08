using SwitchPoint.Calculation.Annuities;
using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Calculation.DbTransfer;

/// <summary>A DB tranche as the engine sees it.</summary>
public sealed record DbTrancheInput(string Name, decimal AccruedAnnualPension, RevaluationRule Revaluation, EscalationRule Escalation, bool IsGmp = false);

/// <summary>Inputs to a DB transfer analysis. See docs/methodology/db-transfer.md.</summary>
public sealed record DbTransferRequest
{
    public required Sex Sex { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required DateOnly CalculationDate { get; init; }
    public required DateOnly DateOfLeaving { get; init; }
    public required int NormalRetirementAge { get; init; }
    public required IReadOnlyList<DbTrancheInput> Tranches { get; init; }
    public required decimal CashEquivalentTransferValue { get; init; }
    public decimal SpousePensionFraction { get; init; } = 0.5m;
    public int GuaranteePeriodYears { get; init; } = 5;
    public decimal PclsCommutationFactor { get; init; } = 20m;
    public decimal MaxPclsFraction { get; init; } = 0.25m;
    public int? EarliestUnreducedAge { get; init; }

    /// <summary>Gilt yields and TVC annuity rates with their as-at date (COBS 19 Annex 4C 1R(2), 2R).</summary>
    public required MarketInputs Market { get; init; }

    public decimal RpiAssumption { get; init; } = 0.03m;
    public decimal CpiAssumption { get; init; } = 0.02m;
    public decimal EarningsAssumption { get; init; } = 0.035m;

    /// <summary>Pre-retirement product charge in the TVC discounting (0.4% since PS20/6).</summary>
    public decimal PreRetirementCharge { get; init; } = 0.004m;

    public decimal AnnuityExpenseLoading { get; init; } = 0.04m;
    public int SpouseAgeGapYears { get; init; } = 3;

    // --- Proposed arrangement (APTA, COBS 19 Annex 4A) ---
    public required ChargeSchedule ProposedCharges { get; init; }
    public decimal? ProposedWeightedOcf { get; init; }

    /// <summary>Adviser-chosen nominal growth reflecting the proposed investments (Annex 4A 1R(1)).</summary>
    public required decimal AptaGrowthRate { get; init; }

    public int PlanEndAge { get; init; } = 100;

    /// <summary>Initial advice fee in cash terms (COBS 9.4.11R payback).</summary>
    public decimal InitialAdviceFee { get; init; }

    /// <summary>Annual charge rate of the available workplace default arrangement, if any (Annex 4A 1R(3)).</summary>
    public decimal? WorkplaceDefaultChargeRate { get; init; }
}

/// <summary>
/// One tranche revalued to normal retirement age and priced as an annuity.
/// </summary>
/// <param name="EscalationInPayment">
/// The escalation used to PRICE the annuity. For an index-linked tranche this is deliberately zero,
/// because the tranche is priced against an index-linked interest rate instead (Annex 4C 1R(2)(d)-(e)).
/// It is therefore not the rate the pension actually increases by, and must not be used to project it.
/// </param>
/// <param name="NominalEscalation">
/// The rate the pension in payment actually increases by each year, after the rule's cap and floor.
/// This is what income comparisons and death benefits project forward.
/// </param>
public sealed record RevaluedTranche(string Name, decimal AccruedAnnualPension, decimal RevaluationRate, int YearsRevalued, decimal PensionAtRetirement, decimal EscalationInPayment, decimal NominalEscalation, decimal AnnuityInterestRate, decimal AnnuityPricePerPound, decimal AnnuityCost, bool IsGmp);

/// <summary>Transfer Value Comparator per COBS 19.1.3AR and Annex 5.</summary>
public sealed record TransferValueComparator(
    decimal CashEquivalentTransferValue,
    decimal EstimatedReplacementCost,
    decimal Difference,
    int RetirementAgeUsed,
    decimal TermYears,
    decimal GiltYieldUsed,
    decimal DiscountRateUsed,
    decimal AnnuityCostAtRetirement,
    decimal PensionAtRetirement,
    IReadOnlyList<RevaluedTranche> Tranches,
    string Wording,
    IReadOnlyList<string> Notes);

public sealed record CriticalYields(decimal TypeAAnnuityMatch, decimal TypeBPclsAndReducedPension, decimal DrawdownHurdleRate, decimal SchemePcls, decimal ResidualPensionAfterPcls, bool Converged);

public sealed record IncomeComparisonRow(int Age, decimal SchemeIncomeNominal, decimal SchemeIncomeReal, decimal DrawdownIncomeReal, decimal ResidualFundReal, decimal SchemeDeathBenefitReal);

public sealed record StressScenario(string Name, decimal SustainableRealIncome, decimal Change);

public sealed record OnePageSummary(decimal InitialAdviceFee, decimal RevaluedMonthlyIncome, int PaybackMonths, decimal FirstYearChargesProposed, decimal OngoingAnnualChargesProposed, decimal FirstYearChargesCeding, decimal? FirstYearChargesWorkplaceDefault);

public sealed record DbTransferResult(
    TransferValueComparator Tvc,
    CriticalYields CriticalYields,
    decimal SustainableRealIncomeFromTransfer,
    IReadOnlyList<IncomeComparisonRow> IncomeComparison,
    IReadOnlyList<StressScenario> StressTests,
    OnePageSummary Summary,
    decimal LifeExpectancyAtRetirement,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Defined benefit transfer analysis: TVC (prescribed), optional critical yields, and APTA income/death-benefit
/// comparisons in real terms with stress tests. See docs/methodology/db-transfer.md.
/// </summary>
public sealed class DbTransferCalculator
{
    private readonly AnnuityPricer _pricer;
    private readonly ProjectionEngine _engine;

    public DbTransferCalculator(AnnuityPricer pricer, ProjectionEngine engine)
    {
        _pricer = pricer ?? throw new ArgumentNullException(nameof(pricer));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public DbTransferResult Calculate(DbTransferRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Validate(r);
        List<string> warnings = [];

        decimal ageNow = (r.CalculationDate.DayNumber - r.DateOfBirth.DayNumber) / 365.25m;
        int retirementAge = RetirementAgeForTvc(r, ageNow, warnings);
        decimal term = ageNow >= r.NormalRetirementAge ? 0m : Math.Max(0m, retirementAge - ageNow);
        int termMonths = (int)Math.Round(term * 12m, MidpointRounding.AwayFromZero);
        int yearsRevalued = CompleteYearsBetween(r.DateOfLeaving, r.DateOfBirth.AddYears(retirementAge));
        int retirementYear = r.DateOfBirth.AddYears(retirementAge).Year;

        // 1–2. Revalue and price each tranche at retirement (Annex 4B 1R(1)–(2), Annex 4C 1R).
        List<RevaluedTranche> tranches = [];
        foreach (DbTrancheInput t in r.Tranches)
        {
            decimal revalRate = t.Revaluation.AnnualRate(r.CpiAssumption, r.RpiAssumption, r.EarningsAssumption);
            decimal atRetirement = t.AccruedAnnualPension * DecimalMath.IntegerPow(1m + revalRate, yearsRevalued);
            (decimal escalation, decimal interest) = AnnuityBasisFor(t.Escalation, r);

            // AnnuityBasisFor returns the PRICING escalation, which is zero for an index-linked
            // tranche because the pricing uses a real interest rate. The pension still increases in
            // payment, so the projection rate has to come from the rule itself.
            decimal nominalEscalation = t.Escalation.AnnualRate(r.CpiAssumption, r.RpiAssumption);
            AnnuityFactorResult factor = _pricer.Factor(new AnnuityRequest(r.Sex, retirementAge, interest, escalation, r.GuaranteePeriodYears, r.SpousePensionFraction, ExpenseLoading: r.AnnuityExpenseLoading, CalendarYear: retirementYear, SpouseAgeGapYears: r.SpouseAgeGapYears));
            tranches.Add(new RevaluedTranche(t.Name, t.AccruedAnnualPension, revalRate, yearsRevalued, atRetirement, escalation, nominalEscalation, interest, factor.PricePerPound, atRetirement * factor.PricePerPound, t.IsGmp));
        }

        decimal pensionAtRetirement = tranches.Sum(t => t.PensionAtRetirement);
        decimal annuityCost = tranches.Sum(t => t.AnnuityCost);

        // 3. Discount to today at the term-appropriate gilt yield net of the 0.4% charge (Annex 4C 2R).
        decimal giltYield = r.Market.GiltYieldForTerm(term);
        decimal discountRate = giltYield - r.PreRetirementCharge;
        decimal tvcValue = annuityCost / RateMath.GrowthFactor(discountRate, term);
        TransferValueComparator tvc = new(
            r.CashEquivalentTransferValue, tvcValue, tvcValue - r.CashEquivalentTransferValue, retirementAge, term, giltYield, discountRate,
            annuityCost, pensionAtRetirement, tranches, Annex5Wording(r.CashEquivalentTransferValue, tvcValue), Annex5Notes());

        // Critical yields (not required since PS18/6; reported for information).
        ProjectionRequest proposed = new()
        {
            StartValue = r.CashEquivalentTransferValue,
            Months = Math.Max(1, termMonths),
            GrowthRate = r.AptaGrowthRate,
            Charges = r.ProposedCharges,
            WeightedOcf = r.ProposedWeightedOcf,
            Inflation = r.CpiAssumption,
            ChargeInflation = r.CpiAssumption,
        };
        bool converged = true;
        decimal typeA = termMonths == 0 ? 0m : SolveGrowth(proposed, annuityCost, r.AptaGrowthRate, ref converged);
        (decimal pcls, decimal residual) = SchemePcls(tranches, r);
        decimal residualCost = annuityCost == 0m ? 0m : annuityCost * (residual / Math.Max(pensionAtRetirement, 0.0000001m));
        decimal typeB = termMonths == 0 ? 0m : SolveGrowth(proposed, pcls + residualCost, r.AptaGrowthRate, ref converged);
        decimal fundAtRetirement = _engine.Project(proposed).FinalValue;
        decimal effectiveCharge = r.ProposedCharges.EffectiveAnnualPercentageCharge(Math.Max(1m, fundAtRetirement), r.ProposedWeightedOcf);
        decimal hurdle = DrawdownHurdle(r.CashEquivalentTransferValue, termMonths, effectiveCharge, tranches, retirementAge, r.PlanEndAge, ref converged);
        CriticalYields cy = new(typeA, typeB, hurdle, pcls, residual, converged);

        // APTA income comparison in real terms (Annex 4A 5R), sustainable drawdown income from the transferred fund.
        decimal netRealRate = RateMath.Real(r.AptaGrowthRate - effectiveCharge, r.CpiAssumption);
        int drawdownYears = Math.Max(1, r.PlanEndAge - retirementAge);
        decimal fundRealAtRetirement = fundAtRetirement / RateMath.GrowthFactor(r.CpiAssumption, term);
        decimal sustainable = LevelIncomeExhausting(fundRealAtRetirement, netRealRate, drawdownYears);
        List<IncomeComparisonRow> comparison = [];
        decimal residualFund = fundRealAtRetirement;
        foreach (int offset in new[] { 0, 5, 10, 15, 20 })
        {
            int age = retirementAge + offset;
            if (age > r.PlanEndAge)
            {
                break;
            }

            decimal schemeNominal = tranches.Sum(t => t.PensionAtRetirement * DecimalMath.IntegerPow(1m + t.NominalEscalation, offset));
            decimal schemeReal = schemeNominal / RateMath.GrowthFactor(r.CpiAssumption, term + offset);
            residualFund = ResidualAfterYears(fundRealAtRetirement, netRealRate, sustainable, offset);
            decimal spouseIncome = schemeNominal * r.SpousePensionFraction;
            decimal spouseAge = r.Sex == Sex.Male ? age - r.SpouseAgeGapYears : age + r.SpouseAgeGapYears;
            Sex spouseSex = r.Sex == Sex.Male ? Sex.Female : Sex.Male;
            decimal deathBenefitNominal = spouseIncome == 0m ? 0m : _pricer.Cost(spouseIncome, new AnnuityRequest(spouseSex, Math.Max(18m, spouseAge), r.Market.TvcAnnuityRateLevel, 0m, 0, 0m, ExpenseLoading: r.AnnuityExpenseLoading, CalendarYear: retirementYear + offset));
            comparison.Add(new IncomeComparisonRow(age, schemeNominal, schemeReal, sustainable, residualFund, deathBenefitNominal / RateMath.GrowthFactor(r.CpiAssumption, term + offset)));
        }

        // Stress tests (Annex 4A 5R).
        List<StressScenario> stress =
        [
            new("Base case", sustainable, 0m),
            Stress("Growth 2% lower", fundRealAtRetirement, RateMath.Real(r.AptaGrowthRate - 0.02m - effectiveCharge, r.CpiAssumption), drawdownYears, sustainable),
            Stress("Inflation 1% higher", fundRealAtRetirement, RateMath.Real(r.AptaGrowthRate - effectiveCharge, r.CpiAssumption + 0.01m), drawdownYears, sustainable),
            Stress("Fund falls 20% at retirement", fundRealAtRetirement * 0.8m, netRealRate, drawdownYears, sustainable),
            Stress("Lives to 105", fundRealAtRetirement, netRealRate, drawdownYears + 5, sustainable),
        ];

        // One-page summary (COBS 9.4.11R).
        decimal revaluedMonthly = pensionAtRetirement / RateMath.GrowthFactor(r.CpiAssumption, term) / 12m;
        int payback = revaluedMonthly <= 0m ? 0 : (int)Math.Ceiling(r.InitialAdviceFee / revaluedMonthly);
        ChargeBreakdown firstYear = r.ProposedCharges.BreakdownFor(r.CashEquivalentTransferValue, 1, r.ProposedWeightedOcf);
        decimal initialAdviser = r.ProposedCharges.AdviserCharges.InitialFor(r.CashEquivalentTransferValue);
        OnePageSummary summary = new(r.InitialAdviceFee, revaluedMonthly, payback, firstYear.Total + initialAdviser, firstYear.Total, 0m,
            r.WorkplaceDefaultChargeRate is { } w ? r.CashEquivalentTransferValue * w : null);

        if (tvcValue > r.CashEquivalentTransferValue)
        {
            warnings.Add($"The TVC shows the same income could cost £{UkFormat.Amount(tvcValue - r.CashEquivalentTransferValue)} more from an insurer than the CETV offered (COBS 19.1.6G: start from the assumption a transfer is not suitable).");
        }

        if (!converged)
        {
            warnings.Add("One or more critical yields did not converge; treat those figures as indicative.");
        }

        decimal expectancy = _pricer.Factor(new AnnuityRequest(r.Sex, retirementAge, 0m, ExpenseLoading: 0m, CalendarYear: retirementYear)).MemberExpectancy;
        return new DbTransferResult(tvc, cy, sustainable, comparison, stress, summary, expectancy, warnings);
    }

    /// <summary>Annex 4C 1R(2)(d)–(e): which annuity rate and escalation to use for a tranche's increases in payment.</summary>
    internal static (decimal Escalation, decimal InterestRate) AnnuityBasisFor(EscalationRule rule, DbTransferRequest r)
    {
        switch (rule.Basis)
        {
            case IndexBasis.None:
                return (0m, r.Market.TvcAnnuityRateLevel);
            case IndexBasis.Fixed:
                return (rule.Rate, r.Market.TvcAnnuityRateLevel);
            case IndexBasis.Rpi:
                return (0m, r.Market.TvcAnnuityRateRpiLinked);
            case IndexBasis.Cpi:
            case IndexBasis.Section148:
                return (0m, r.Market.TvcAnnuityRateCpiLinked);
            case IndexBasis.LpiRpi:
                if (rule.Cap is { } capR && capR <= 0.035m)
                {
                    return (capR, r.Market.TvcAnnuityRateLevel);
                }

                if (rule.Floor is { } floorR && floorR >= 0.035m)
                {
                    return (floorR, r.Market.TvcAnnuityRateLevel);
                }

                return (0m, r.Market.TvcAnnuityRateRpiLinked);
            case IndexBasis.LpiCpi:
                if (rule.Cap is { } capC && capC <= 0.025m)
                {
                    return (capC, r.Market.TvcAnnuityRateLevel);
                }

                if (rule.Floor is { } floorC && floorC >= 0.03m)
                {
                    return (floorC, r.Market.TvcAnnuityRateLevel);
                }

                return (0m, r.Market.TvcAnnuityRateCpiLinked);
            default:
                throw new ArgumentOutOfRangeException(nameof(rule), rule.Basis, "Unknown index basis.");
        }
    }

    private static int RetirementAgeForTvc(DbTransferRequest r, decimal ageNow, List<string> warnings)
    {
        if (ageNow >= r.NormalRetirementAge)
        {
            warnings.Add("Client is past normal retirement age; the TVC uses the retirement age assumed in the CETV (COBS 19.1.3AR(3)).");
            return (int)Math.Floor(ageNow);
        }

        if (r.EarliestUnreducedAge is { } early && early < r.NormalRetirementAge && early > ageNow)
        {
            warnings.Add($"The scheme allows an unreduced pension from age {early}; the TVC is calculated at that age (COBS 19.1.3AR(4)).");
            return early;
        }

        return r.NormalRetirementAge;
    }

    private static int CompleteYearsBetween(DateOnly from, DateOnly to)
    {
        if (to <= from)
        {
            return 0;
        }

        int years = to.Year - from.Year;
        if (to < from.AddYears(years))
        {
            years--;
        }

        return years;
    }

    private decimal SolveGrowth(ProjectionRequest proposed, decimal target, decimal guess, ref bool converged)
    {
        if (target <= 0m)
        {
            return -0.99m;
        }

        try
        {
            return RootFinder.Solve(x => _engine.Project(proposed with { GrowthRate = Math.Max(-0.99m, x) }).FinalValue - target, guess - 0.15m, guess + 0.15m).Value;
        }
        catch (RootNotBracketedException)
        {
            converged = false;
            return guess;
        }
        catch (RootNotConvergedException)
        {
            converged = false;
            return guess;
        }
    }

    /// <summary>Growth needed for the fund to pay the scheme pension (escalating) from retirement to the plan end and be exhausted then.</summary>
    private static decimal DrawdownHurdle(decimal cetv, int termMonths, decimal chargeRate, IReadOnlyList<RevaluedTranche> tranches, int retirementAge, int planEndAge, ref bool converged)
    {
        int years = Math.Max(1, planEndAge - retirementAge);
        decimal termYears = termMonths / 12m;
        decimal Residual(decimal g)
        {
            decimal net = g - chargeRate;
            decimal fund = cetv * RateMath.GrowthFactor(Math.Max(-0.99m, net), termYears);
            for (int y = 0; y < years; y++)
            {
                decimal income = tranches.Sum(t => t.PensionAtRetirement * DecimalMath.IntegerPow(1m + t.NominalEscalation, y));
                fund = (fund - income) * (1m + net);
            }

            return fund;
        }

        try
        {
            return RootFinder.Solve(Residual, 0m, 0.15m).Value;
        }
        catch (RootNotBracketedException)
        {
            converged = false;
            return 0m;
        }
        catch (RootNotConvergedException)
        {
            converged = false;
            return 0m;
        }
    }

    /// <summary>Level real income that exhausts <paramref name="fund"/> over <paramref name="years"/> at real net rate <paramref name="rate"/> (paid at the start of each year).</summary>
    internal static decimal LevelIncomeExhausting(decimal fund, decimal rate, int years)
    {
        if (fund <= 0m || years <= 0)
        {
            return 0m;
        }

        if (Math.Abs(rate) < 1e-12m)
        {
            return fund / years;
        }

        decimal v = 1m / (1m + rate);
        decimal annuityDue = (1m - DecimalMath.IntegerPow(v, years)) / (1m - v);
        return fund / annuityDue;
    }

    private static decimal ResidualAfterYears(decimal fund, decimal rate, decimal income, int years)
    {
        decimal f = fund;
        for (int y = 0; y < years; y++)
        {
            f = (f - income) * (1m + rate);
        }

        return Math.Max(0m, f);
    }

    private static StressScenario Stress(string name, decimal fund, decimal rate, int years, decimal baseline)
    {
        decimal income = LevelIncomeExhausting(fund, rate, years);
        return new StressScenario(name, income, income - baseline);
    }

    /// <summary>Scheme PCLS by commutation at the scheme factor, capped at the HMRC 25% of value test (20 × pension + lump sum) and never commuting GMP.</summary>
    internal static (decimal Pcls, decimal ResidualPension) SchemePcls(IReadOnlyList<RevaluedTranche> tranches, DbTransferRequest r)
    {
        decimal p = tranches.Sum(t => t.PensionAtRetirement);
        decimal gmp = tranches.Where(t => t.IsGmp).Sum(t => t.PensionAtRetirement);
        if (p <= 0m || r.PclsCommutationFactor <= 0m || r.MaxPclsFraction <= 0m)
        {
            return (0m, p);
        }

        // PCLS = f × (PCLS + 20 × residual) with PCLS = c × CF, residual = P − c  ⇒  c = 20fP / (CF(1 − f) + 20f)
        decimal f = r.MaxPclsFraction;
        decimal c = 20m * f * p / ((r.PclsCommutationFactor * (1m - f)) + (20m * f));
        c = Math.Min(c, Math.Max(0m, p - gmp));
        return (c * r.PclsCommutationFactor, p - c);
    }

    private static string Annex5Wording(decimal cetv, decimal replacementCost) =>
        $"You have been offered a cash equivalent transfer value of £{UkFormat.Amount(cetv)} in exchange for you giving up any future claims to a pension from the scheme. " +
        $"Will I be better or worse off by transferring? It could cost you £{UkFormat.Amount(replacementCost)} to obtain a comparable level of income from an insurer. " +
        $"This means the same retirement income could cost you £{UkFormat.Amount(replacementCost - cetv)} more by transferring.";

    private static IReadOnlyList<string> Annex5Notes() =>
    [
        "The estimated replacement cost is based on the income the scheme would pay at its normal retirement age (or the earliest age an unreduced pension is available), including a spouse's pension, for an average healthy person, using today's costs.",
        "The estimated replacement value takes into account investment returns after product charges that you might obtain from risk-free investments.",
        "No allowance has been made for taxation or adviser charges prior to benefits commencing.",
    ];

    private static void Validate(DbTransferRequest r)
    {
        ArgumentNullException.ThrowIfNull(r.Tranches);
        ArgumentNullException.ThrowIfNull(r.Market);
        ArgumentNullException.ThrowIfNull(r.ProposedCharges);
        if (r.Tranches.Count == 0)
        {
            throw new ArgumentException("At least one tranche is required.", nameof(r));
        }

        if (r.CashEquivalentTransferValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), r.CashEquivalentTransferValue, "CETV must be positive.");
        }

        if (r.NormalRetirementAge is < 50 or > 75 || r.PlanEndAge <= r.NormalRetirementAge)
        {
            throw new ArgumentOutOfRangeException(nameof(r), "Retirement age must be 50–75 and the plan end age must exceed it.");
        }

        if (r.SpousePensionFraction is < 0m or > 1m || r.MaxPclsFraction is < 0m or > 0.25m || r.PclsCommutationFactor < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), "Spouse fraction, PCLS fraction or commutation factor out of range.");
        }

        if (r.DateOfLeaving < r.DateOfBirth.AddYears(16) || r.CalculationDate < r.DateOfBirth)
        {
            throw new ArgumentOutOfRangeException(nameof(r), "Dates are inconsistent with the date of birth.");
        }
    }
}
