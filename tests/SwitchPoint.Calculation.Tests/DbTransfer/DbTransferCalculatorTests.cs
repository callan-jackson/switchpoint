using SwitchPoint.Calculation.Annuities;
using SwitchPoint.Calculation.DbTransfer;
using SwitchPoint.Calculation.Mortality;
using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Calculation.Tests.DbTransfer;

public class DbTransferCalculatorTests
{
    private static readonly DbTransferCalculator Calc = new(new AnnuityPricer(GompertzMakehamLifeTable.Default), new ProjectionEngine());

    // Illustrative market inputs (gilt yields ~4.5%, TVC annuity rates) as at 15 August 2026.
    private static readonly MarketInputs Market = new(0.042m, 0.044m, 0.046m, 0.047m, 0.008m, 0.040m, 0.010m, new DateOnly(2026, 8, 15));

    private static DbTransferRequest Request(decimal cetv = 450_000m, int nra = 65, DateOnly? dob = null, int? earliestUnreduced = null, decimal aptaGrowth = 0.05m) => new()
    {
        Sex = Sex.Male,
        DateOfBirth = dob ?? new DateOnly(1971, 6, 15),   // 55 at calculation
        CalculationDate = new DateOnly(2026, 9, 6),
        DateOfLeaving = new DateOnly(2016, 3, 31),
        NormalRetirementAge = nra,
        Tranches =
        [
            new DbTrancheInput("Pre-97 GMP", 1_500m, RevaluationRule.Fixed(0.0475m), EscalationRule.None, IsGmp: true),
            new DbTrancheInput("97–05 excess", 8_000m, RevaluationRule.StatutoryPre2009, EscalationRule.StatutoryPre2005),
            new DbTrancheInput("Post-05", 6_000m, RevaluationRule.StatutoryPost2009, EscalationRule.StatutoryPost2005),
        ],
        CashEquivalentTransferValue = cetv,
        EarliestUnreducedAge = earliestUnreduced,
        Market = Market,
        ProposedCharges = new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.0025m), FundCharge = FundChargeBasis.Explicit(0.0022m), AdviserCharges = new AdviserCharge(initialRate: 0.02m, ongoingRate: 0.0075m) },
        AptaGrowthRate = aptaGrowth,
        InitialAdviceFee = 9_000m,
        WorkplaceDefaultChargeRate = 0.0075m,
    };

    [Fact]
    public void Tvc_revalues_prices_and_discounts_per_cobs_19()
    {
        DbTransferResult r = Calc.Calculate(Request());
        TransferValueComparator tvc = r.Tvc;
        Assert.Equal(65, tvc.RetirementAgeUsed);
        Assert.InRange(tvc.TermYears, 9.7m, 9.8m);
        Assert.Equal(0.044m, tvc.GiltYieldUsed);           // 5–10 year band
        Assert.Equal(0.040m, tvc.DiscountRateUsed);         // less 0.4% charge (PS20/6)
        Assert.Equal(3, tvc.Tranches.Count);

        RevaluedTranche gmp = tvc.Tranches[0];
        Assert.Equal(0.0475m, gmp.RevaluationRate);
        Assert.Equal(20, gmp.YearsRevalued);                 // 31 Mar 2016 → 15 Jun 2036: 20 complete years
        Assert.Equal(0.040m, gmp.AnnuityInterestRate);       // level annuity rate
        Assert.Equal(0m, gmp.EscalationInPayment);

        RevaluedTranche pre09 = tvc.Tranches[1];
        Assert.Equal(0.02m, pre09.RevaluationRate);          // LPI(CPI) 5% cap with CPI 2%
        Assert.Equal(0.018m, pre09.AnnuityInterestRate);     // CPI-linked = RPI-linked + 1%

        RevaluedTranche post05 = tvc.Tranches[2];
        Assert.Equal(0.02m, post05.RevaluationRate);
        Assert.Equal(0.025m, post05.EscalationInPayment);    // LPI(CPI) capped at 2.5% ⇒ fixed-increase basis at the cap
        Assert.Equal(0.040m, post05.AnnuityInterestRate);

        decimal expectedPension = tvc.Tranches.Sum(t => t.AccruedAnnualPension * DecimalMath.IntegerPow(1m + t.RevaluationRate, t.YearsRevalued));
        Assert.Equal(expectedPension, tvc.PensionAtRetirement);
        Assert.Equal(tvc.Tranches.Sum(t => t.AnnuityCost), tvc.AnnuityCostAtRetirement);
        Assert.InRange(Math.Abs(tvc.EstimatedReplacementCost - (tvc.AnnuityCostAtRetirement / RateMath.GrowthFactor(0.040m, tvc.TermYears))), 0m, 0.01m);
        Assert.Contains("It could cost you £", tvc.Wording, StringComparison.Ordinal);
        Assert.Equal(3, tvc.Notes.Count);
        // £15.5k accrued revalues to about £24.6k at 65; at ~4.4% gilt yields the replacement cost is roughly 13× that
        // (around £330k), and would exceed £500k at the ~1% yields seen before 2022.
        Assert.InRange(tvc.EstimatedReplacementCost, 250_000m, 600_000m);
    }

    [Fact]
    public void Earliest_unreduced_age_moves_the_tvc_retirement_age()
    {
        DbTransferResult r = Calc.Calculate(Request(earliestUnreduced: 60));
        Assert.Equal(60, r.Tvc.RetirementAgeUsed);
        Assert.Contains(r.Warnings, w => w.Contains("unreduced", StringComparison.Ordinal));
    }

    [Fact]
    public void Client_past_nra_uses_current_age()
    {
        DbTransferResult r = Calc.Calculate(Request(dob: new DateOnly(1958, 1, 1)));
        Assert.Equal(68, r.Tvc.RetirementAgeUsed);
        Assert.Equal(0m, r.Tvc.TermYears);
        Assert.Equal(r.Tvc.AnnuityCostAtRetirement, r.Tvc.EstimatedReplacementCost);
    }

    [Fact]
    public void Critical_yields_are_consistent()
    {
        DbTransferResult r = Calc.Calculate(Request());
        CriticalYields cy = r.CriticalYields;
        Assert.True(cy.Converged);
        // Type B (PCLS + reduced pension) needs less than Type A exactly when the scheme's commutation factor (20)
        // is below the blended annuity price per £1 of pension, i.e. cash at 20:1 is cheaper than the income given up.
        decimal pricePerPound = r.Tvc.AnnuityCostAtRetirement / r.Tvc.PensionAtRetirement;
        Assert.Equal(pricePerPound > 20m, cy.TypeBPclsAndReducedPension < cy.TypeAAnnuityMatch);
        Assert.True(cy.SchemePcls > 0m);
        Assert.True(cy.ResidualPensionAfterPcls < r.Tvc.PensionAtRetirement);
        // GMP is never commuted.
        Assert.True(cy.ResidualPensionAfterPcls >= r.Tvc.Tranches[0].PensionAtRetirement);
        // HMRC 25% test: PCLS = 25% × (PCLS + 20 × residual).
        decimal value = cy.SchemePcls + (20m * cy.ResidualPensionAfterPcls);
        Assert.InRange(Math.Abs(cy.SchemePcls - (0.25m * value)), 0m, 0.01m);
        Assert.InRange(cy.DrawdownHurdleRate, 0.0m, 0.15m);
        Assert.True(cy.TypeAAnnuityMatch is > 0.0m and < 0.15m);
    }

    [Fact]
    public void Apta_comparison_is_real_terms_and_stress_tests_move_the_right_way()
    {
        DbTransferResult r = Calc.Calculate(Request());
        Assert.Equal(5, r.IncomeComparison.Count);
        Assert.Equal([65, 70, 75, 80, 85], r.IncomeComparison.Select(x => x.Age));
        foreach (IncomeComparisonRow row in r.IncomeComparison)
        {
            Assert.True(row.SchemeIncomeReal < row.SchemeIncomeNominal);
            Assert.Equal(r.SustainableRealIncomeFromTransfer, row.DrawdownIncomeReal);
            Assert.True(row.ResidualFundReal >= 0m);
            Assert.True(row.SchemeDeathBenefitReal > 0m);
        }

        Assert.True(r.IncomeComparison[0].ResidualFundReal > r.IncomeComparison[^1].ResidualFundReal);
        StressScenario lowerGrowth = r.StressTests.Single(s => s.Name == "Growth 2% lower");
        StressScenario shock = r.StressTests.Single(s => s.Name == "Fund falls 20% at retirement");
        StressScenario longer = r.StressTests.Single(s => s.Name == "Lives to 105");
        StressScenario inflation = r.StressTests.Single(s => s.Name == "Inflation 1% higher");
        Assert.True(lowerGrowth.Change < 0m && shock.Change < 0m && longer.Change < 0m && inflation.Change < 0m);
        Assert.InRange(Math.Abs(shock.SustainableRealIncome - (0.8m * r.SustainableRealIncomeFromTransfer)), 0m, 0.01m);
        Assert.InRange(r.LifeExpectancyAtRetirement, 17m, 22m);
    }

    [Fact]
    public void One_page_summary_payback_months_follow_cobs_9_4_11r()
    {
        DbTransferResult r = Calc.Calculate(Request());
        OnePageSummary s = r.Summary;
        decimal expectedMonthly = r.Tvc.PensionAtRetirement / RateMath.GrowthFactor(0.02m, r.Tvc.TermYears) / 12m;
        Assert.InRange(Math.Abs(s.RevaluedMonthlyIncome - expectedMonthly), 0m, 0.0001m);
        Assert.Equal((int)Math.Ceiling(9_000m / expectedMonthly), s.PaybackMonths);
        Assert.Equal(450_000m * 0.0075m, s.FirstYearChargesWorkplaceDefault);
        Assert.Equal(0m, s.FirstYearChargesCeding);
        Assert.True(s.FirstYearChargesProposed > s.OngoingAnnualChargesProposed); // includes the initial adviser charge
        Assert.InRange(s.OngoingAnnualChargesProposed, 450_000m * 0.0122m - 1m, 450_000m * 0.0122m + 1m); // 0.25% + 0.22% + 0.75%
    }

    [Fact]
    public void Level_income_exhausting_fund_matches_annuity_due_formula()
    {
        decimal income = DbTransferCalculator.LevelIncomeExhausting(100_000m, 0.03m, 20);
        decimal fund = 100_000m;
        for (int y = 0; y < 20; y++)
        {
            fund = (fund - income) * 1.03m;
        }

        Assert.InRange(Math.Abs(fund), 0m, 0.001m);
        Assert.Equal(5_000m, DbTransferCalculator.LevelIncomeExhausting(100_000m, 0m, 20));
        Assert.Equal(0m, DbTransferCalculator.LevelIncomeExhausting(0m, 0.03m, 20));
    }

    [Fact]
    public void Annuity_basis_follows_annex_4c_thresholds()
    {
        DbTransferRequest r = Request();
        Assert.Equal((0m, 0.040m), DbTransferCalculator.AnnuityBasisFor(EscalationRule.None, r));
        Assert.Equal((0.03m, 0.040m), DbTransferCalculator.AnnuityBasisFor(EscalationRule.Fixed(0.03m), r));
        Assert.Equal((0m, 0.008m), DbTransferCalculator.AnnuityBasisFor(EscalationRule.Rpi(), r));
        Assert.Equal((0m, 0.018m), DbTransferCalculator.AnnuityBasisFor(EscalationRule.Cpi(), r));
        Assert.Equal((0.035m, 0.040m), DbTransferCalculator.AnnuityBasisFor(EscalationRule.LpiCappedRpi(0.035m), r)); // cap ≤ 3.5% → fixed at cap
        Assert.Equal((0m, 0.008m), DbTransferCalculator.AnnuityBasisFor(EscalationRule.LpiCappedRpi(0.05m), r));      // cap 5% → RPI-linked
        Assert.Equal((0.025m, 0.040m), DbTransferCalculator.AnnuityBasisFor(EscalationRule.LpiCappedCpi(0.025m), r));
        Assert.Equal((0m, 0.018m), DbTransferCalculator.AnnuityBasisFor(EscalationRule.LpiCappedCpi(0.05m), r));
        Assert.Equal((0.03m, 0.040m), DbTransferCalculator.AnnuityBasisFor(EscalationRule.Cpi(cap: 0.05m, floor: 0.03m), r)); // floor ≥ 3% → fixed at floor
    }

    [Fact]
    public void Warnings_and_validation()
    {
        DbTransferResult r = Calc.Calculate(Request(cetv: 100_000m));
        Assert.Contains(r.Warnings, w => w.Contains("COBS 19.1.6G", StringComparison.Ordinal));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calc.Calculate(Request(cetv: 0m)));
        Assert.Throws<ArgumentException>(() => Calc.Calculate(Request() with { Tranches = [] }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calc.Calculate(Request() with { PlanEndAge = 60 }));
    }

    [Fact]
    public void Higher_cetv_never_changes_the_tvc_but_lowers_critical_yields()
    {
        DbTransferResult low = Calc.Calculate(Request(cetv: 300_000m));
        DbTransferResult high = Calc.Calculate(Request(cetv: 600_000m));
        Assert.Equal(low.Tvc.EstimatedReplacementCost, high.Tvc.EstimatedReplacementCost);
        Assert.True(high.CriticalYields.TypeAAnnuityMatch < low.CriticalYields.TypeAAnnuityMatch);
        Assert.True(high.CriticalYields.DrawdownHurdleRate < low.CriticalYields.DrawdownHurdleRate);
        Assert.True(high.SustainableRealIncomeFromTransfer > low.SustainableRealIncomeFromTransfer);
    }
}
