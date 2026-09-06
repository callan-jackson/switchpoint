using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Market;

namespace SwitchPoint.Domain.Tests.Market;

public class MarketTests
{
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Asset_allocation_must_sum_to_one()
    {
        _ = new AssetAllocation(0.6m, 0.3m, 0.05m, 0.05m, 0m);
        Assert.Throws<DomainException>(() => new AssetAllocation(0.6m, 0.3m, 0.05m, 0.06m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetAllocation(1.2m, -0.2m, 0m, 0m, 0m));
        AssetAllocation blend = AssetAllocation.Blend([(AssetAllocation.AllEquity, 0.5m), (AssetAllocation.AllCash, 0.5m)]);
        Assert.Equal(0.5m, blend.Equity);
        Assert.Equal(0.5m, blend.Cash);
        Assert.Throws<DomainException>(() => AssetAllocation.Blend([(AssetAllocation.AllEquity, 0m)]));
    }

    [Fact]
    public void Product_charge_versions_are_closed_and_pinned()
    {
        Product p = new(Guid.NewGuid(), Guid.NewGuid(), "Platform SIPP", WrapperTypes.Sipp | WrapperTypes.Isa, Now);
        ProductChargeVersion v1 = p.AddChargeVersion(new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.003m) }, new DateOnly(2025, 1, 1), "https://example.com/2025", new DateOnly(2025, 1, 15), DataQuality.Verified, Now);
        ProductChargeVersion v2 = p.AddChargeVersion(new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.0025m) }, new DateOnly(2026, 4, 6), "https://example.com/2026", new DateOnly(2026, 4, 6), DataQuality.Verified, Now);
        Assert.Equal(1, v1.Version);
        Assert.Equal(2, v2.Version);
        Assert.Equal(new DateOnly(2026, 4, 5), p.Version(1).EffectiveTo);
        Assert.Same(v2, p.CurrentCharges);
        Assert.Equal(1, p.ChargesOn(new DateOnly(2026, 1, 1))!.Version);
        Assert.Equal(2, p.ChargesOn(new DateOnly(2026, 9, 1))!.Version);
        Assert.Null(p.ChargesOn(new DateOnly(2024, 1, 1)));
        Assert.Throws<DomainException>(() => p.AddChargeVersion(ChargeSchedule.None, new DateOnly(2026, 1, 1), null, new DateOnly(2026, 1, 1), DataQuality.Indicative, Now));
        Assert.Throws<DomainException>(() => p.Version(9));
        Assert.True(p.Supports(WrapperTypes.Sipp));
        Assert.False(p.Supports(WrapperTypes.OnshoreBond));
        Assert.Throws<DomainException>(() => new Product(Guid.NewGuid(), Guid.NewGuid(), "X", WrapperTypes.None, Now));
    }

    [Fact]
    public void Fund_validates_isin_and_ratings()
    {
        Fund f = new(Guid.NewGuid(), "GB00B3X7QG63", "Vanguard LifeStrategy 60% Equity", "Vanguard", FundType.Oeic, 0.0022m, new AssetAllocation(0.6m, 0.4m, 0m, 0m, 0m), Now, srri: 4);
        Assert.Equal("GB00B3X7QG63", f.Isin);
        Assert.Throws<DomainException>(() => new Fund(Guid.NewGuid(), "BAD", "x", "y", FundType.Oeic, 0.001m, AssetAllocation.AllEquity, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => f.UpdateStatistics(new FundStatistics(null, null, null, null, null, null, null, 7, null), null, null, Now));
        f.UpdateCharges(0.0023m, 0.0004m, new DateOnly(2026, 8, 31), "https://example.com", Now);
        Assert.Equal(0.0023m, f.Ocf);
    }

    [Fact]
    public void Model_portfolio_blends_costs()
    {
        ModelPortfolio m = new(Guid.NewGuid(), Guid.NewGuid(), "Balanced 5", 5, 0.0015m, Now);
        m.ReplaceHoldings([
            new ModelPortfolioHolding(Guid.NewGuid(), "GB00B3X7QG63", "A", 0.5m, 0.0022m),
            new ModelPortfolioHolding(Guid.NewGuid(), "IE00B4L5Y983", "B", 0.5m, 0.0012m),
        ], Now);
        Assert.Equal(0.0017m, m.BlendedOcf);
        Assert.Equal(0.0032m, m.TotalInvestmentCharge);
    }

    [Fact]
    public void Assumption_sets_validate_and_version()
    {
        MarketInputs inputs = new(0.04m, 0.042m, 0.045m, 0.047m, 0.005m, 0.035m, 0.006m, new DateOnly(2026, 9, 1));
        AssumptionSet fca = new(Guid.NewGuid(), null, "FCA standard 2026/27", true, Now, 0.02m, 0.05m, 0.08m, 0.02m, 0.035m, 0.03m, 0.02m, 0.004m, 0.04m, 3, MortalityBasis.OnsNationalLifeTables2020_22, 0.025m, "2026/27", ProjectionBasis.Real, inputs);
        Assert.Throws<DomainException>(() => fca.Update(e => e.GrowthIntermediate = 0.06m, Now));
        AssumptionSet firm = fca.CopyForFirm(Guid.NewGuid(), Guid.NewGuid(), "Our defaults", Now);
        Assert.False(firm.IsFcaStandard);
        firm.Update(e => e.GrowthIntermediate = 0.045m, Now);
        Assert.Equal(2, firm.Version);
        Assert.Equal(0.045m, firm.GrowthIntermediate);
        Assert.Throws<DomainException>(() => firm.Update(e => e.GrowthIntermediate = 0.09m, Now)); // above higher
        Assert.Equal(0.045m, firm.GrowthIntermediate);
        Assert.Equal(0.015m, inputs.TvcAnnuityRateCpiLinked);
        Assert.Equal(0.04m, inputs.GiltYieldForTerm(5m));
        Assert.Equal(0.042m, inputs.GiltYieldForTerm(5.1m));
        Assert.Equal(0.047m, inputs.GiltYieldForTerm(30m));
    }

    [Fact]
    public void Capital_market_assumptions_validate_matrix()
    {
        AssetClassAssumption[] classes = [new(AssetClass.UkEquity, 0.07m, 0.16m), new(AssetClass.Cash, 0.03m, 0.01m)];
        decimal[,] good = { { 1m, 0.1m }, { 0.1m, 1m } };
        decimal[,] asym = { { 1m, 0.1m }, { 0.2m, 1m } };
        decimal[,] badDiag = { { 0.9m, 0.1m }, { 0.1m, 1m } };
        _ = new CapitalMarketAssumptions(classes, good, new DateOnly(2026, 1, 1), "test");
        Assert.Throws<DomainException>(() => new CapitalMarketAssumptions(classes, asym, new DateOnly(2026, 1, 1), "test"));
        Assert.Throws<DomainException>(() => new CapitalMarketAssumptions(classes, badDiag, new DateOnly(2026, 1, 1), "test"));
        Assert.Throws<DomainException>(() => new CapitalMarketAssumptions(classes, new decimal[3, 3], new DateOnly(2026, 1, 1), "test"));
    }
}
