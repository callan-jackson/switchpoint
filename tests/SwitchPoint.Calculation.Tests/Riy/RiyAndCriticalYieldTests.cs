using SwitchPoint.Calculation.CriticalYield;
using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Calculation.Riy;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Calculation.Tests.Riy;

public class RiyAndCriticalYieldTests
{
    private static readonly ProjectionEngine Engine = new();
    private static readonly ReductionInYieldCalculator Riy = new(Engine);
    private static readonly CriticalYieldCalculator Cy = new(Engine, Riy);

    private static ProjectionRequest Request(ChargeSchedule charges, decimal start = 100_000m, int months = 240, decimal growth = 0.05m, IReadOnlyList<ContributionSpec>? contributions = null) => new()
    {
        StartValue = start,
        Months = months,
        GrowthRate = growth,
        Charges = charges,
        Contributions = contributions ?? [],
        WeightedOcf = 0.0022m,
        Inflation = 0.02m,
    };

    [Fact]
    public void Flat_percentage_charge_gives_riy_close_to_the_charge()
    {
        // 0.75% a year accrued monthly: the effective deduction is 1 − (1 − 0.0075/12)^12 = 0.7474% of the grown fund,
        // so C = 1.05 × (1 − 0.007474) − 1 = 4.215% and RIY = B − C = 0.785% (the COBS construction, not the headline charge).
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Flat(0.0075m) };
        RiyResult r = Riy.Calculate(Request(c));
        Assert.InRange(r.ProductRiy, 0.00784m, 0.00786m);
        Assert.Equal(r.ProductRiy, r.TotalRiy);
        Assert.Equal(0.008m, r.ProductRiyRounded);
        Assert.Equal(0.05m, r.GrowthRate);
        Assert.True(r.ValueBeforeCharges > r.ValueAfterAllCharges);
        Assert.Equal("Product charges reduce investment growth after price inflation from 5.0% to 4.2%.", r.ProductSentence(true));
    }

    [Fact]
    public void Adviser_charges_appear_only_in_total_riy()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Flat(0.003m), FundCharge = FundChargeBasis.FromHoldings, AdviserCharges = new AdviserCharge(initialRate: 0.02m, ongoingRate: 0.005m) };
        RiyResult r = Riy.Calculate(Request(c));
        Assert.InRange(r.ProductRiy, 0.0054m, 0.0056m);       // (0.3% + 0.22%) × 1.05
        Assert.True(r.TotalRiy > r.ProductRiy + 0.005m);       // ongoing 0.5% plus the initial 2% spread over 20 years
        Assert.InRange(r.TotalRiy, 0.0110m, 0.0125m);
    }

    [Fact]
    public void No_charges_means_zero_riy()
    {
        RiyResult r = Riy.Calculate(Request(ChargeSchedule.None));
        Assert.Equal(0m, r.ProductRiy);
        Assert.Equal(0m, r.TotalRiy);
        Assert.Equal(r.ValueBeforeCharges, r.ValueAfterAllCharges);
    }

    [Fact]
    public void Riy_construction_reproduces_the_charged_value_at_rate_c()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Marginal((250_000m, 0.0025m), (null, 0.0015m)), FundCharge = FundChargeBasis.FromHoldings, FixedCharges = [new FixedCharge(75m, Frequency.Annually)] };
        ProjectionRequest req = Request(c, contributions: [new ContributionSpec(300m, Frequency.Monthly)]);
        RiyResult r = Riy.Calculate(req);
        decimal reproduced = Engine.Project(req with { Charges = ChargeSchedule.None, GrowthRate = r.RateAfterProductCharges }).FinalValue;
        Assert.InRange(Math.Abs(reproduced - r.ValueAfterProductCharges), 0m, 0.01m);
    }

    [Fact]
    public void Effect_of_charges_table_years_follow_cobs_13()
    {
        Assert.Equal([1, 3, 5, 10, 15, 20], ReductionInYieldCalculator.TableYears(240));
        Assert.Equal([1, 3, 5, 10, 15, 20, 25, 30, 35, 37], ReductionInYieldCalculator.TableYears(444));
        Assert.Equal([1, 2], ReductionInYieldCalculator.TableYears(18));
        Assert.Equal([1], ReductionInYieldCalculator.TableYears(6));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 25], ReductionInYieldCalculator.TableYears(300, drawdown: true));
    }

    [Fact]
    public void Effect_of_charges_rows_are_ordered_and_consistent()
    {
        ChargeSchedule c = new() { PlatformCharge = TieredCharge.Flat(0.004m), AdviserCharges = new AdviserCharge(ongoingRate: 0.005m) };
        RiyResult r = Riy.Calculate(Request(c, contributions: [new ContributionSpec(100m, Frequency.Monthly)]));
        Assert.Equal(6, r.EffectOfCharges.Count);
        foreach (EffectOfChargesRow row in r.EffectOfCharges)
        {
            Assert.True(row.BeforeCharges >= row.PlanAndInvestmentChargesOnly);
            Assert.True(row.PlanAndInvestmentChargesOnly >= row.AfterAllCharges);
            Assert.Equal(row.BeforeCharges - row.AfterAllCharges, row.EffectOfDeductionsToDate);
            Assert.Equal(1_200m * row.Year, row.PaymentsToDate);
        }
    }

    private static CriticalYieldRequest SwitchRequest(ChargeSchedule ceding, ChargeSchedule receiving, decimal exitPenaltyPct = 0m, Guarantees? guarantees = null)
    {
        ProjectionRequest cedingProjection = Request(ceding, start: 100_000m, contributions: [new ContributionSpec(200m, Frequency.Monthly)]);
        decimal net = 100_000m * (1m - exitPenaltyPct);
        return new CriticalYieldRequest(
            [new CedingSchemeInput("Old personal pension", cedingProjection, net, guarantees ?? Guarantees.None)],
            Request(receiving, start: 0m),
            0.02m, 0.05m, 0.08m, 0.02m);
    }

    [Fact]
    public void Cheaper_receiving_product_has_critical_yield_below_growth_and_positive_headroom()
    {
        ChargeSchedule ceding = new() { ProductCharge = TieredCharge.Flat(0.015m) };   // 1.5% legacy AMC
        ChargeSchedule receiving = new() { PlatformCharge = TieredCharge.Flat(0.0025m), FundCharge = FundChargeBasis.FromHoldings }; // 0.25% + 0.22%
        CriticalYieldResult r = Cy.Calculate(SwitchRequest(ceding, receiving));
        Assert.True(r.Intermediate.CriticalYield < 0.05m);
        Assert.True(r.Intermediate.Headroom > 0.009m && r.Intermediate.Headroom < 0.012m);
        Assert.True(r.Intermediate.ProjectedGain > 0m);
        Assert.Equal(1, r.Intermediate.BreakEvenYear);
        Assert.Equal(SwitchVerdict.SwitchCandidate, r.Schemes[0].Verdict);
        Assert.True(r.Lower.CriticalYield < r.Intermediate.CriticalYield && r.Intermediate.CriticalYield < r.Higher.CriticalYield);
        Assert.InRange(Math.Abs(r.Intermediate.CriticalYieldReal - RateMath.Real(r.Intermediate.CriticalYield, 0.02m)), 0m, 1e-20m);
        Assert.True(r.Intermediate.Converged);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Dearer_receiving_product_with_initial_fee_needs_outperformance()
    {
        ChargeSchedule ceding = new() { ProductCharge = TieredCharge.Flat(0.004m) };
        ChargeSchedule receiving = new() { PlatformCharge = TieredCharge.Flat(0.0035m), FundCharge = FundChargeBasis.FromHoldings, AdviserCharges = new AdviserCharge(initialRate: 0.03m, ongoingRate: 0.0075m) };
        CriticalYieldResult r = Cy.Calculate(SwitchRequest(ceding, receiving, exitPenaltyPct: 0.02m));
        Assert.True(r.Intermediate.CriticalYield > 0.05m);
        Assert.True(r.Intermediate.Headroom < 0m);
        Assert.Null(r.Intermediate.BreakEvenYear);
        Assert.Equal(SwitchVerdict.Retain, r.Schemes[0].Verdict);
        Assert.Equal(98_000m, r.TotalNetTransferValue);
        Assert.Equal(98_000m * 0.03m, r.InitialAdviserCharge);
        Assert.Contains(r.Warnings, w => w.Contains("negative headroom", StringComparison.Ordinal));
    }

    [Fact]
    public void Identical_charges_give_critical_yield_equal_to_growth()
    {
        ChargeSchedule same = new() { PlatformCharge = TieredCharge.Flat(0.005m) };
        CriticalYieldResult r = Cy.Calculate(SwitchRequest(same, same));
        Assert.InRange(Math.Abs(r.Intermediate.CriticalYield - 0.05m), 0m, 1e-6m);
        Assert.Equal(SwitchVerdict.Consider, r.Schemes[0].Verdict);
    }

    [Fact]
    public void Guarantees_force_a_referral()
    {
        ChargeSchedule ceding = new() { ProductCharge = TieredCharge.Flat(0.015m) };
        ChargeSchedule receiving = new() { PlatformCharge = TieredCharge.Flat(0.0025m) };
        CriticalYieldResult r = Cy.Calculate(SwitchRequest(ceding, receiving, guarantees: new Guarantees(guaranteedAnnuityRate: 0.09m)));
        Assert.Equal(SwitchVerdict.Refer, r.Schemes[0].Verdict);
        Assert.True(r.AnyGuaranteesFlagged);
        Assert.Contains(r.Warnings, w => w.Contains("guarantees", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Multiple_ceding_schemes_consolidate()
    {
        ProjectionRequest a = Request(new ChargeSchedule { ProductCharge = TieredCharge.Flat(0.01m) }, start: 60_000m);
        ProjectionRequest b = Request(new ChargeSchedule { ProductCharge = TieredCharge.Flat(0.012m) }, start: 40_000m, contributions: [new ContributionSpec(150m, Frequency.Monthly)]);
        CriticalYieldRequest req = new(
            [new CedingSchemeInput("A", a, 60_000m, Guarantees.None), new CedingSchemeInput("B", b, 39_000m, Guarantees.None)],
            Request(new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.003m), FundCharge = FundChargeBasis.FromHoldings }, start: 0m),
            0.02m, 0.05m, 0.08m, 0.02m);
        CriticalYieldResult r = Cy.Calculate(req);
        Assert.Equal(99_000m, r.TotalNetTransferValue);
        Assert.Equal(2, r.Schemes.Count);
        Assert.True(r.Intermediate.Headroom > 0m);
        Assert.Equal(Engine.Project(a).FinalValue + Engine.Project(b).FinalValue, r.Intermediate.ExistingValueAtRetirement);
    }

    [Fact]
    public void Validation_rejects_mismatched_terms_and_bad_rates()
    {
        ProjectionRequest ceding = Request(ChargeSchedule.None, months: 120);
        CriticalYieldRequest badTerm = new([new CedingSchemeInput("X", ceding, 1m, Guarantees.None)], Request(ChargeSchedule.None, months: 240), 0.02m, 0.05m, 0.08m, 0.02m);
        Assert.Throws<ArgumentException>(() => Cy.Calculate(badTerm));
        CriticalYieldRequest badRates = new([new CedingSchemeInput("X", Request(ChargeSchedule.None), 1m, Guarantees.None)], Request(ChargeSchedule.None), 0.08m, 0.05m, 0.02m, 0.02m);
        Assert.Throws<ArgumentException>(() => Cy.Calculate(badRates));
        Assert.Throws<ArgumentException>(() => Cy.Calculate(new CriticalYieldRequest([], Request(ChargeSchedule.None), 0.02m, 0.05m, 0.08m, 0.02m)));
    }
}
