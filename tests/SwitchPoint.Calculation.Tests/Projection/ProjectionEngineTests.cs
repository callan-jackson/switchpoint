using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Calculation.Tests.Projection;

public class ProjectionEngineTests
{
    private static readonly ProjectionEngine Engine = new();

    private static ProjectionRequest Basic(decimal start = 100_000m, int months = 12, decimal growth = 0.05m, ChargeSchedule? charges = null) => new()
    {
        StartValue = start,
        Months = months,
        GrowthRate = growth,
        Charges = charges ?? ChargeSchedule.None,
        Inflation = 0.02m,
    };

    [Fact]
    public void No_growth_no_charges_no_contributions_returns_start_value_exactly()
    {
        ProjectionResult r = Engine.Project(Basic(growth: 0m, months: 480));
        Assert.Equal(100_000m, r.FinalValue);
        Assert.Equal(0m, r.TotalCharges.Total);
        Assert.Equal(40, r.Schedule.Count);
    }

    [Fact]
    public void Five_percent_for_a_year_with_no_charges_is_start_times_1_05()
    {
        ProjectionResult r = Engine.Project(Basic());
        Assert.InRange(Math.Abs(r.FinalValue - 105_000m), 0m, 1e-10m);
        Assert.InRange(Math.Abs(r.FinalValueReal - (105_000m / 1.02m)), 0m, 1e-8m);
    }

    [Fact]
    public void Percentage_charge_with_no_growth_compounds_monthly()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Flat(0.012m) };
        ProjectionResult r = Engine.Project(Basic(growth: 0m, charges: c));
        decimal expected = 100_000m * DecimalMath.IntegerPow(1m - (0.012m / 12m), 12);
        Assert.InRange(Math.Abs(r.FinalValue - expected), 0m, 1e-10m);
        Assert.InRange(Math.Abs(r.TotalCharges.Platform - (100_000m - expected)), 0m, 1e-10m);
    }

    [Fact]
    public void Monthly_contributions_arrive_at_the_start_of_the_month_and_earn_growth()
    {
        ProjectionRequest req = Basic(start: 0m, growth: 0.05m) with { Contributions = [new ContributionSpec(100m, Frequency.Monthly)] };
        ProjectionResult r = Engine.Project(req);
        decimal m = RateMath.AnnualToMonthly(0.05m);
        // Annuity-due: Σ 100 (1+m)^k for k = 1..12
        decimal expected = 0m;
        for (int k = 1; k <= 12; k++)
        {
            expected += 100m * DecimalMath.IntegerPow(1m + m, k);
        }

        Assert.InRange(Math.Abs(r.FinalValue - expected), 0m, 1e-10m);
        Assert.Equal(1_200m, r.TotalContributions);
    }

    [Fact]
    public void Contribution_frequencies_escalation_and_grossing_up()
    {
        ProjectionRequest req = Basic(start: 0m, growth: 0m, months: 24) with
        {
            Contributions =
            [
                new ContributionSpec(1_200m, Frequency.Annually, EscalationRate: 0.10m),
                new ContributionSpec(300m, Frequency.Quarterly),
                new ContributionSpec(80m, Frequency.Monthly, GrossOfTaxRelief: false),
                new ContributionSpec(5_000m, Frequency.Single, StartMonth: 6),
                new ContributionSpec(50m, Frequency.Monthly, StartMonth: 13, EndMonth: 18),
            ],
        };
        ProjectionResult r = Engine.Project(req);
        decimal expected = 1_200m + 1_320m + (300m * 8) + (100m * 24) + 5_000m + (50m * 6);
        Assert.Equal(expected, r.FinalValue);
        Assert.Equal(expected, r.TotalContributions);
    }

    [Fact]
    public void Initial_adviser_charge_is_deducted_before_investment()
    {
        ChargeSchedule c = new() { AdviserCharges = new AdviserCharge(initialRate: 0.02m, initialAmount: 250m) };
        ProjectionResult r = Engine.Project(Basic(growth: 0m, charges: c));
        Assert.Equal(2_250m, r.InitialAdviserCharge);
        Assert.Equal(97_750m, r.FinalValue);
        Assert.Equal(2_250m, r.TotalCharges.AdviserInitial);
        ProjectionResult skipped = Engine.Project(Basic(growth: 0m, charges: c) with { ApplyInitialAdviserCharge = false });
        Assert.Equal(100_000m, skipped.FinalValue);
    }

    [Fact]
    public void Fixed_charges_by_frequency_scope_and_indexation()
    {
        ChargeSchedule c = new()
        {
            FixedCharges =
            [
                new FixedCharge(10m, Frequency.Monthly),
                new FixedCharge(30m, Frequency.Quarterly),
                new FixedCharge(120m, Frequency.Annually, Indexation.Cpi),
                new FixedCharge(200m, Frequency.Annually, Indexation.None, FixedChargeScope.Drawdown),
                new FixedCharge(50m, Frequency.Single),
            ],
        };
        ProjectionResult accumulation = Engine.Project(Basic(growth: 0m, months: 24, charges: c) with { ChargeInflation = 0.03m });
        decimal expectedFixed = (10m * 24) + (30m * 8) + 120m + (120m * 1.03m) + 50m;
        Assert.InRange(Math.Abs(accumulation.TotalCharges.Fixed - expectedFixed), 0m, 1e-10m);

        ProjectionResult drawdown = Engine.Project(Basic(growth: 0m, months: 24, charges: c) with { ChargeInflation = 0.03m, InDrawdown = true });
        Assert.InRange(Math.Abs(drawdown.TotalCharges.Fixed - (expectedFixed + 400m)), 0m, 1e-10m);
    }

    [Fact]
    public void Tiered_platform_charge_uses_marginal_bands_on_the_current_value()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Marginal((250_000m, 0.0025m), (null, 0.0015m)) };
        ProjectionResult r = Engine.Project(Basic(start: 500_000m, growth: 0m, months: 1, charges: c));
        // (250k × 0.25% + 250k × 0.15%) / 12 = (625 + 375)/12
        Assert.InRange(Math.Abs(r.TotalCharges.Platform - (1_000m / 12m)), 0m, 1e-10m);
    }

    [Fact]
    public void Household_linking_lowers_the_effective_tier_rate()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Marginal((250_000m, 0.0025m), (null, 0.0015m)) };
        ProjectionResult alone = Engine.Project(Basic(start: 100_000m, growth: 0m, months: 1, charges: c));
        ProjectionResult linked = Engine.Project(Basic(start: 100_000m, growth: 0m, months: 1, charges: c) with { HouseholdLinkedValue = 900_000m });
        Assert.True(linked.TotalCharges.Platform < alone.TotalCharges.Platform);
    }

    [Fact]
    public void Fund_charges_transaction_costs_and_ongoing_adviser_fee_accrue_monthly()
    {
        ChargeSchedule c = new() { FundCharge = FundChargeBasis.FromHoldings, TransactionCosts = 0.001m, AdviserCharges = new AdviserCharge(ongoingRate: 0.005m, ongoingAmount: 120m) };
        ProjectionResult r = Engine.Project(Basic(growth: 0m, months: 1, charges: c) with { WeightedOcf = 0.002m });
        Assert.InRange(Math.Abs(r.TotalCharges.Fund - (100_000m * 0.002m / 12m)), 0m, 1e-12m);
        Assert.InRange(Math.Abs(r.TotalCharges.Transaction - (100_000m * 0.001m / 12m)), 0m, 1e-12m);
        Assert.InRange(Math.Abs(r.TotalCharges.AdviserOngoing - ((500m + 120m) / 12m)), 0m, 1e-12m);
        Assert.Throws<DomainException>(() => Engine.Project(Basic(charges: c)));
    }

    [Fact]
    public void Legacy_allocation_rate_and_bid_offer_spread_reduce_invested_contributions()
    {
        ChargeSchedule c = new() { AllocationRate = 0.95m, BidOfferSpread = 0.05m };
        ProjectionRequest req = Basic(start: 0m, growth: 0m, months: 1, charges: c) with { Contributions = [new ContributionSpec(1_000m, Frequency.Monthly)] };
        ProjectionResult r = Engine.Project(req);
        Assert.Equal(902.5m, r.FinalValue);
        Assert.Equal(97.5m, r.TotalCharges.AllocationAndSpread);
    }

    [Fact]
    public void Exit_penalty_at_end_uses_years_in_force()
    {
        ChargeSchedule c = new() { ExitPenalty = ExitPenaltySchedule.Declining((5m, 0.05m), (10m, 0.02m)) };
        ProjectionResult r = Engine.Project(Basic(growth: 0m, months: 24, charges: c) with { ApplyExitPenaltyAtEnd = true, YearsInForceAtStart = 4m });
        Assert.Equal(2_000m, r.ExitPenaltyAtEnd); // 6 years in force → 2%
        Assert.Equal(98_000m, r.FinalValue);
        Assert.Equal(98_000m, r.Schedule[^1].Value);
    }

    [Fact]
    public void Fund_exhausted_by_fixed_fees_floors_at_zero_and_records_unpaid()
    {
        ChargeSchedule c = new() { FixedCharges = [new FixedCharge(500m, Frequency.Monthly)] };
        ProjectionResult r = Engine.Project(Basic(start: 1_200m, growth: 0m, months: 12, charges: c));
        Assert.Equal(0m, r.FinalValue);
        Assert.Equal(1_200m, r.TotalCharges.Fixed);
        Assert.Equal(4_800m, r.UnpaidCharges);
    }

    [Fact]
    public void Large_fund_discount_is_a_negative_charge()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Flat(0.003m), LargeFundDiscounts = [new LargeFundDiscount(1_000_000m, 0.001m)] };
        ProjectionResult r = Engine.Project(Basic(start: 2_000_000m, growth: 0m, months: 1, charges: c));
        Assert.True(r.TotalCharges.Discounts < 0m);
        Assert.InRange(Math.Abs(r.TotalCharges.Total - (2_000_000m * 0.002m / 12m)), 0m, 1e-10m);
    }

    [Fact]
    public void With_and_without_charges_share_contribution_and_growth_handling()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Flat(0.003m), AdviserCharges = new AdviserCharge(ongoingRate: 0.005m) };
        ProjectionRequest req = Basic(months: 120, charges: c) with { Contributions = [new ContributionSpec(200m, Frequency.Monthly)] };
        ChargeEffectProjections p = Engine.ProjectWithAndWithoutCharges(req);
        Assert.Equal(p.BeforeCharges.TotalContributions, p.AllCharges.TotalContributions);
        Assert.True(p.BeforeCharges.FinalValue > p.PlanAndInvestmentChargesOnly.FinalValue);
        Assert.True(p.PlanAndInvestmentChargesOnly.FinalValue > p.AllCharges.FinalValue);
        Assert.Equal(0m, p.PlanAndInvestmentChargesOnly.TotalCharges.AdviserOngoing);
    }

    [Fact]
    public void Rows_are_emitted_per_year_and_final_month()
    {
        ProjectionResult r = Engine.Project(Basic(months: 30));
        Assert.Equal(3, r.Schedule.Count);
        Assert.Equal([12, 24, 30], r.Schedule.Select(x => x.Month));
        Assert.Equal(r.Schedule[1], r.AtYear(2));
        Assert.Equal(r.Schedule[^1], r.AtYear(99));
    }

    [Fact]
    public void Validation_rejects_bad_inputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Project(Basic(start: -1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Project(Basic(months: 2000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Project(Basic(growth: -1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Project(Basic() with { Contributions = [new ContributionSpec(-1m, Frequency.Monthly)] }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Project(Basic() with { ReliefAtSourceRate = 1m }));
    }

    [Fact]
    public void Determinism_identical_inputs_give_identical_outputs()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Marginal((250_000m, 0.0025m), (null, 0.0015m)), FundCharge = FundChargeBasis.Explicit(0.0022m) };
        ProjectionRequest req = Basic(months: 480, charges: c) with { Contributions = [new ContributionSpec(500m, Frequency.Monthly, 0.03m)] };
        Assert.Equal(Engine.Project(req).FinalValue, Engine.Project(req).FinalValue);
    }

    [Fact]
    public void Forty_year_projection_runs_fast()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Marginal((250_000m, 0.0025m), (null, 0.0015m)), FundCharge = FundChargeBasis.Explicit(0.0022m), AdviserCharges = new AdviserCharge(ongoingRate: 0.005m) };
        ProjectionRequest req = Basic(months: 480, charges: c) with { Contributions = [new ContributionSpec(500m, Frequency.Monthly, 0.03m)] };
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            Engine.Project(req);
        }

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 2_000, $"20 projections took {sw.ElapsedMilliseconds} ms");
    }

    [Property(MaxTest = 100)]
    public Property Charges_never_increase_final_value_and_growth_is_monotonic()
    {
        Gen<decimal> starts = Gen.Choose(0, 1_000_000).Select(v => (decimal)v);
        Gen<decimal> rates = Gen.Choose(-500, 1500).Select(v => v / 10_000m);
        Gen<decimal> charges = Gen.Choose(0, 300).Select(v => v / 10_000m);
        Gen<(decimal Start, decimal G1, decimal G2, decimal Charge)> combined = from start in starts from g1 in rates from g2 in rates from ch in charges select (start, g1, g2, ch);
        return Prop.ForAll(combined.ToArbitrary(), t =>
        {
            (decimal start, decimal g1, decimal g2, decimal ch) = t;
            ChargeSchedule s = new() { PlatformCharge = TieredCharge.Flat(ch) };
            decimal withCharges = Engine.Project(Basic(start, 120, g1, s)).FinalValue;
            decimal without = Engine.Project(Basic(start, 120, g1)).FinalValue;
            decimal other = Engine.Project(Basic(start, 120, g2)).FinalValue;
            bool chargesReduce = withCharges <= without + 1e-9m;
            bool monotonic = g1 == g2 || start == 0m || (g1 < g2) == (without < other);
            return chargesReduce && monotonic;
        });
    }
}
