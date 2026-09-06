using SwitchPoint.Calculation.Cashflow;
using SwitchPoint.Calculation.MonteCarlo;
using SwitchPoint.Calculation.Tax;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Market;

namespace SwitchPoint.Calculation.Tests.Cashflow;

public class CashflowAndMonteCarloTests
{
    private static readonly CashflowEngine Engine = new();
    private static readonly DateOnly Start = new(2026, 9, 6);

    private static PersonInput Person(int retirementAge = 67, DateOnly? dob = null) =>
        new("Alex", dob ?? new DateOnly(1971, 5, 1), Sex.Male, TaxRegime.RestOfUk, retirementAge, 241.30m, 35);

    private static ChargeSchedule Platform() => new() { PlatformCharge = TieredCharge.Flat(0.0025m), FundCharge = FundChargeBasis.Explicit(0.0022m) };

    private static CashflowRequest Typical(int planEndAge = 100) => new()
    {
        StartDate = Start,
        Person = Person(),
        PlanEndAge = planEndAge,
        Incomes = [new CashflowIncome(new PlanIncome("Salary", IncomeKind.Employment, 55_000m, 55, null, 0.035m))],
        Expenses = [new PlanExpensePhase("Working life", 34_000m, 55, 66), new PlanExpensePhase("Retirement", 32_000m, 67, null)],
        Assets =
        [
            new CashflowAsset(new PlanAsset("SIPP", PlanAssetKind.UncrystallisedPension, 350_000m, 0.05m, Platform(), AnnualContribution: 6_000m, EmployerContribution: 4_000m), 0, new AssetAllocation(0.6m, 0.35m, 0m, 0.05m, 0m)),
            new CashflowAsset(new PlanAsset("Stocks & shares ISA", PlanAssetKind.Isa, 80_000m, 0.05m, Platform())),
            new CashflowAsset(new PlanAsset("Cash", PlanAssetKind.Cash, 25_000m, 0.03m, ChargeSchedule.None)),
        ],
        Tax = TaxYears.Y2026_27,
    };

    [Fact]
    public void Runs_from_current_age_to_plan_end_with_one_row_per_year()
    {
        CashflowResult r = Engine.Run(Typical());
        Assert.Equal(45, r.Rows.Count);           // 55 → 99 inclusive
        Assert.Equal(55, r.Rows[0].Age);
        Assert.Equal(99, r.Rows[^1].Age);
        Assert.All(r.Rows, row => Assert.True(row.TotalAssets >= 0m));
    }

    [Fact]
    public void Employment_income_stops_at_retirement_and_state_pension_starts_at_spa()
    {
        CashflowResult r = Engine.Run(Typical());
        Assert.True(r.Rows.Single(x => x.Age == 66).EmploymentIncome > 0m);
        Assert.Equal(0m, r.Rows.Single(x => x.Age == 67).EmploymentIncome);
        // Born 1 May 1971 → SPA 67 on 1 May 2038; the plan year starting 6 Sep 2037 (age 66) gets a part-year, age 67 a full year.
        Assert.Equal(0m, r.Rows.Single(x => x.Age == 65).StatePensionIncome);
        Assert.True(r.Rows.Single(x => x.Age == 66).StatePensionIncome > 0m);
        decimal full = r.Rows.Single(x => x.Age == 67).StatePensionIncome;
        Assert.True(full > r.Rows.Single(x => x.Age == 66).StatePensionIncome);
        // Uprated from £241.30/week at 3.5% for 12 years.
        decimal expected = 241.30m * 52m * (decimal)Math.Pow(1.035, 12);
        Assert.InRange(Math.Abs(full - expected) / expected, 0m, 0.0001m);
    }

    [Fact]
    public void Working_years_pay_tax_and_ni_and_contribute_to_the_pension()
    {
        CashflowResult r = Engine.Run(Typical());
        CashflowRow first = r.Rows[0];
        Assert.Equal(55_000m, first.EmploymentIncome);
        Assert.True(first.NationalInsurance > 0m);
        Assert.True(first.IncomeTax > 0m);
        Assert.Equal(10_000m, first.Contributions);
        Assert.True(first.Surplus > 0m);
        Assert.Equal(0m, first.Shortfall);
        Assert.Empty(first.Warnings);
        // Retired: no NI.
        Assert.Equal(0m, r.Rows.Single(x => x.Age == 70).NationalInsurance);
    }

    [Fact]
    public void Retirement_draws_the_gap_from_assets_in_strategy_order_and_taxes_pension_income()
    {
        CashflowResult r = Engine.Run(Typical());
        CashflowRow year70 = r.Rows.Single(x => x.Age == 70);
        Assert.True(year70.Expenses > 0m);
        Assert.True(year70.PensionWithdrawalsTaxable + year70.TaxFreeCash + year70.IsaWithdrawals + year70.CashWithdrawals > 0m);
        // Phased drawdown: 25% of each uncrystallised withdrawal is tax-free.
        if (year70.PensionWithdrawalsTaxable > 0m)
        {
            Assert.InRange(year70.TaxFreeCash / (year70.TaxFreeCash + year70.PensionWithdrawalsTaxable), 0.2499m, 0.2501m);
        }

        // Net income should cover expenses (no shortfall) in a well-funded plan.
        Assert.Equal(0m, year70.Shortfall);
        Assert.InRange(Math.Abs(year70.NetIncome - year70.Expenses), 0m, 1m);
        Assert.True(r.Succeeds);
        Assert.True(r.LumpSumAllowanceUsed > 0m);
        Assert.True(r.LumpSumAllowanceUsed <= 268_275m);
    }

    [Fact]
    public void Pcls_up_front_moves_a_quarter_to_cash_at_retirement()
    {
        CashflowRequest req = Typical() with { Strategy = PlanStrategy.Default with { Crystallisation = CrystallisationChoice.PclsUpFront } };
        CashflowResult r = Engine.Run(req);
        CashflowRow atRetirement = r.Rows.Single(x => x.Age == 67);
        Assert.Contains(atRetirement.Warnings, w => w.Contains("tax-free cash", StringComparison.Ordinal));
        Assert.Contains(atRetirement.Assets, a => a.Kind == PlanAssetKind.Drawdown);
        Assert.True(r.LumpSumAllowanceUsed > 100_000m);
    }

    [Fact]
    public void Under_funded_plan_records_first_shortfall_age()
    {
        CashflowRequest req = Typical() with { Expenses = [new PlanExpensePhase("Lavish", 120_000m, 55, null)], Incomes = [] };
        CashflowResult r = Engine.Run(req);
        Assert.False(r.Succeeds);
        Assert.NotNull(r.FirstShortfallAge);
        Assert.True(r.FirstShortfallAge < 70);
        Assert.True(r.TotalShortfall > 0m);
        Assert.Equal(0m, r.LegacyAtEnd);
    }

    [Fact]
    public void Pension_cannot_be_accessed_below_normal_minimum_pension_age()
    {
        CashflowRequest req = Typical() with
        {
            Person = Person(dob: new DateOnly(1980, 1, 1)),       // 46 at start
            Incomes = [],
            Expenses = [new PlanExpensePhase("Spend", 40_000m, 40, null)],
            Assets = [new CashflowAsset(new PlanAsset("SIPP", PlanAssetKind.UncrystallisedPension, 500_000m, 0.05m, Platform()))],
        };
        CashflowResult r = Engine.Run(req);
        CashflowRow age50 = r.Rows.Single(x => x.Age == 50);
        Assert.Equal(0m, age50.PensionWithdrawalsTaxable);
        Assert.True(age50.Shortfall > 0m);
        CashflowRow age58 = r.Rows.Single(x => x.Age == 58); // NMPA is 57 from April 2028
        Assert.True(age58.PensionWithdrawalsTaxable > 0m);
    }

    [Fact]
    public void Gia_withdrawals_incur_capital_gains_tax_on_the_gain_element()
    {
        CashflowRequest req = Typical() with
        {
            Incomes = [],
            Expenses = [new PlanExpensePhase("Spend", 30_000m, 55, null)],
            Assets = [new CashflowAsset(new PlanAsset("GIA", PlanAssetKind.GeneralInvestmentAccount, 400_000m, 0.05m, ChargeSchedule.None, CostBasis: 100_000m))],
            Strategy = PlanStrategy.Default with { WithdrawalOrder = [PlanAssetKind.GeneralInvestmentAccount] },
        };
        CashflowResult r = Engine.Run(req);
        CashflowRow first = r.Rows[0];
        Assert.True(first.GiaWithdrawals > 30_000m);
        Assert.True(first.CapitalGainsTax > 0m);
        Assert.InRange(Math.Abs(first.GiaWithdrawals - first.CapitalGainsTax - 30_000m), 0m, 1m);
    }

    [Fact]
    public void Surplus_is_reinvested_into_isa_up_to_the_allowance()
    {
        CashflowResult r = Engine.Run(Typical());
        CashflowRow first = r.Rows[0];
        AssetValue isa = first.Assets.Single(a => a.Kind == PlanAssetKind.Isa);
        Assert.True(isa.Value > 80_000m * 1.04m);
    }

    [Fact]
    public void Allowance_warnings_surface()
    {
        CashflowRequest req = Typical() with
        {
            Assets = [new CashflowAsset(new PlanAsset("SIPP", PlanAssetKind.UncrystallisedPension, 100_000m, 0.05m, Platform(), AnnualContribution: 60_000m, EmployerContribution: 30_000m))],
        };
        CashflowResult r = Engine.Run(req);
        Assert.Contains(r.Rows[0].Warnings, w => w.Contains("annual allowance", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Rows[0].Warnings, w => w.Contains("relievable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Sustainable_spend_is_the_largest_level_real_spend_without_shortfall()
    {
        CashflowRequest req = Typical() with { Incomes = [] };
        decimal spend = Engine.SustainableSpend(req);
        Assert.InRange(spend, 10_000m, 60_000m);
        int startAge = 55;
        Assert.True(Engine.Run(req with { Expenses = [new PlanExpensePhase("x", spend, startAge, null)] }).Succeeds);
        Assert.False(Engine.Run(req with { Expenses = [new PlanExpensePhase("x", spend + 500m, startAge, null)] }).Succeeds);
    }

    [Fact]
    public void Determinism_and_validation()
    {
        CashflowResult a = Engine.Run(Typical());
        CashflowResult b = Engine.Run(Typical());
        Assert.Equal(a.LegacyAtEnd, b.LegacyAtEnd);
        Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Run(Typical() with { PlanEndAge = 40 }));
    }

    private static CapitalMarketAssumptions Cma() => new(
        [
            new AssetClassAssumption(AssetClass.GlobalEquity, 0.065m, 0.15m),
            new AssetClassAssumption(AssetClass.GovernmentBonds, 0.035m, 0.07m),
            new AssetClassAssumption(AssetClass.Property, 0.045m, 0.12m),
            new AssetClassAssumption(AssetClass.Cash, 0.03m, 0.01m),
            new AssetClassAssumption(AssetClass.Alternatives, 0.05m, 0.10m),
        ],
        new decimal[,]
        {
            { 1m, 0.2m, 0.5m, 0.0m, 0.4m },
            { 0.2m, 1m, 0.1m, 0.2m, 0.1m },
            { 0.5m, 0.1m, 1m, 0.0m, 0.3m },
            { 0.0m, 0.2m, 0.0m, 1m, 0.0m },
            { 0.4m, 0.1m, 0.3m, 0.0m, 1m },
        },
        new DateOnly(2026, 1, 1), "SwitchPoint illustrative CMA");

    [Fact]
    public void Monte_carlo_is_reproducible_and_produces_ordered_percentiles()
    {
        MonteCarloSimulator sim = new(Engine);
        MonteCarloRequest req = new(Typical(), Cma(), Seed: 2026, Paths: 200);
        MonteCarloResult a = sim.Run(req);
        MonteCarloResult b = sim.Run(req);
        Assert.Equal(a.ProbabilityOfSuccess, b.ProbabilityOfSuccess);
        Assert.Equal(a.TotalAssetsReal[10].P50, b.TotalAssetsReal[10].P50);
        Assert.Equal(45, a.TotalAssetsReal.Count);
        foreach (PercentileRow row in a.TotalAssetsReal)
        {
            Assert.True(row.P5 <= row.P10 && row.P10 <= row.P25 && row.P25 <= row.P50 && row.P50 <= row.P75 && row.P75 <= row.P90 && row.P90 <= row.P95);
        }

        Assert.InRange(a.ProbabilityOfSuccess, 0m, 1m);
        Assert.True(a.TotalAssetsReal[20].P95 > a.TotalAssetsReal[20].P5);
        Assert.NotEqual(a.TotalAssetsReal[10].P50, sim.Run(req with { Seed = 7 }).TotalAssetsReal[10].P50);
    }

    [Fact]
    public void Monte_carlo_with_zero_volatility_reproduces_the_deterministic_plan()
    {
        CapitalMarketAssumptions flat = new(
            [new AssetClassAssumption(AssetClass.GlobalEquity, 0.05m, 0m), new AssetClassAssumption(AssetClass.GovernmentBonds, 0.05m, 0m), new AssetClassAssumption(AssetClass.Cash, 0.03m, 0m)],
            new decimal[,] { { 1m, 0m, 0m }, { 0m, 1m, 0m }, { 0m, 0m, 1m } }, new DateOnly(2026, 1, 1), "flat");
        MonteCarloSimulator sim = new(Engine);
        // Every asset (and every class) at 5% so the zero-volatility paths equal the deterministic plan exactly.
        CashflowRequest plan = Typical() with
        {
            Assets = [.. Typical().Assets.Select(a => new CashflowAsset(a.Asset with { GrowthRate = 0.05m }, a.PersonIndex, a.Allocation))],
        };
        flat = new CapitalMarketAssumptions(
            [new AssetClassAssumption(AssetClass.GlobalEquity, 0.05m, 0m), new AssetClassAssumption(AssetClass.GovernmentBonds, 0.05m, 0m), new AssetClassAssumption(AssetClass.Cash, 0.05m, 0m)],
            new decimal[,] { { 1m, 0m, 0m }, { 0m, 1m, 0m }, { 0m, 0m, 1m } }, new DateOnly(2026, 1, 1), "flat");
        CashflowResult det = Engine.Run(plan);
        MonteCarloResult mc = sim.Run(new MonteCarloRequest(plan, flat, 1, 100));
        Assert.InRange(Math.Abs(mc.TotalAssetsReal[^1].P50 - det.LegacyAtEndReal) / Math.Max(1m, det.LegacyAtEndReal), 0m, 0.001m);
        Assert.True(mc.Conservativeness.MedianIsNoLessConservative);
        Assert.Equal(det.Succeeds ? 1m : 0m, mc.ProbabilityOfSuccess);
    }

    [Fact]
    public void Cholesky_rejects_non_positive_definite_matrices()
    {
        decimal[,] bad = { { 1m, 1.2m }, { 1.2m, 1m } };
        Assert.Throws<ArgumentException>(() => MonteCarloSimulator.Cholesky(bad, 2));
        double[,] l = MonteCarloSimulator.Cholesky(new decimal[,] { { 1m, 0.5m }, { 0.5m, 1m } }, 2);
        Assert.Equal(1.0, l[0, 0], 12);
        Assert.Equal(0.5, l[1, 0], 12);
        Assert.Equal(Math.Sqrt(0.75), l[1, 1], 12);
    }

    [Fact]
    public void Monte_carlo_validates_path_count()
    {
        MonteCarloSimulator sim = new(Engine);
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Run(new MonteCarloRequest(Typical(), Cma(), 1, Paths: 10)));
    }
}
