using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Tests.Charges;

public class ChargeScheduleTests
{
    private static ChargeSchedule Platform() => new()
    {
        PlatformCharge = TieredCharge.Marginal((250_000m, 0.0025m), (null, 0.0015m)),
        FundCharge = FundChargeBasis.FromHoldings,
        TransactionCosts = 0.0005m,
        AdviserCharges = new AdviserCharge(initialRate: 0.02m, ongoingRate: 0.0075m),
        FixedCharges = [new FixedCharge(120m, Frequency.Annually, Indexation.Cpi, FixedChargeScope.Drawdown, "Drawdown fee")],
        DealingCharges = new DealingCharges(fundDealAmount: 1.50m, expectedFundDealsPerYear: 12),
        SwitchCharge = new SwitchCharge(0m, 0),
        LargeFundDiscounts = [new LargeFundDiscount(1_000_000m, 0.0005m)],
    };

    [Fact]
    public void Breakdown_totals_sum_components()
    {
        ChargeBreakdown b = Platform().BreakdownFor(300_000m, year: 1, weightedOcf: 0.0022m);
        Assert.Equal(700m, b.Platform);        // 250k×0.25% + 50k×0.15%
        Assert.Equal(0m, b.Product);
        Assert.Equal(660m, b.Fund);            // 300k × 0.22%
        Assert.Equal(150m, b.Transaction);
        Assert.Equal(2250m, b.AdviserOngoing); // 0.75%
        Assert.Equal(0m, b.Fixed);             // drawdown fee not in drawdown
        Assert.Equal(18m, b.Dealing);
        Assert.Equal(0m, b.Discounts);
        Assert.Equal(700m + 660m + 150m + 2250m + 18m, b.Total);
        Assert.Equal(b.Total / 300_000m, b.EffectiveRate);
    }

    [Fact]
    public void Drawdown_fee_applies_only_in_drawdown_and_is_indexed()
    {
        ChargeSchedule s = Platform();
        Assert.Equal(120m, s.BreakdownFor(100_000m, 1, 0.002m, chargeInflation: 0.02m, inDrawdown: true).Fixed);
        Assert.Equal(120m * 1.02m * 1.02m, s.BreakdownFor(100_000m, 3, 0.002m, chargeInflation: 0.02m, inDrawdown: true).Fixed);
    }

    [Fact]
    public void Large_fund_discount_is_negative_charge()
    {
        ChargeBreakdown b = Platform().BreakdownFor(1_500_000m, 1, 0.002m);
        Assert.Equal(-750m, b.Discounts);
        Assert.True(b.Total < b.Platform + b.Fund + b.Transaction + b.AdviserOngoing + b.Dealing);
    }

    [Fact]
    public void From_holdings_basis_requires_weighted_ocf() =>
        Assert.Throws<DomainException>(() => Platform().BreakdownFor(100_000m, 1, weightedOcf: null));

    [Fact]
    public void Explicit_fund_basis_ignores_supplied_ocf()
    {
        ChargeSchedule s = new() { FundCharge = FundChargeBasis.Explicit(0.01m) };
        Assert.Equal(1000m, s.BreakdownFor(100_000m, 1, weightedOcf: 0.002m).Fund);
    }

    [Fact]
    public void None_schedule_has_zero_charges()
    {
        Assert.Equal(0m, ChargeSchedule.None.BreakdownFor(123_456m).Total);
        Assert.False(ChargeSchedule.None.HasExitPenalty);
    }

    [Fact]
    public void Effective_percentage_uses_year_one()
    {
        ChargeSchedule s = new() { PlatformCharge = TieredCharge.Flat(0.003m), AdviserCharges = new AdviserCharge(ongoingRate: 0.005m) };
        Assert.Equal(0.008m, s.EffectiveAnnualPercentageCharge(50_000m));
    }

    [Fact]
    public void Legacy_fields_are_validated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChargeSchedule { AllocationRate = 0.2m });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChargeSchedule { BidOfferSpread = 1.5m });
        Assert.Throws<DomainException>(() => new ChargeSchedule { LargeFundDiscounts = [new LargeFundDiscount(500_000m, 0.001m), new LargeFundDiscount(250_000m, 0.002m)] });
    }

    [Fact]
    public void Structural_equality_holds_for_equivalent_schedules()
    {
        Assert.Equal(Platform(), Platform());
        Assert.Equal(Platform().GetHashCode(), Platform().GetHashCode());
        Assert.NotEqual(Platform(), Platform() with { TransactionCosts = 0.001m });
    }

    [Fact]
    public void Adviser_initial_charge_combines_rate_and_amount()
    {
        AdviserCharge a = new(initialRate: 0.01m, initialAmount: 500m, ongoingRate: 0.005m, ongoingAmount: 100m);
        Assert.Equal(1500m, a.InitialFor(100_000m));
        Assert.Equal(600m, a.OngoingAnnualFor(100_000m));
        Assert.True(AdviserCharge.None.IsNone);
    }
}
