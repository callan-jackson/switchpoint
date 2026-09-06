using SwitchPoint.Calculation.Annuities;
using SwitchPoint.Calculation.Mortality;
using SwitchPoint.Domain.Clients;

namespace SwitchPoint.Calculation.Tests.Annuities;

public class AnnuityPricerTests
{
    private static readonly AnnuityPricer Pricer = new(GompertzMakehamLifeTable.Default);

    [Fact]
    public void Life_table_is_calibrated_to_ons_2020_22_at_65()
    {
        GompertzMakehamLifeTable t = GompertzMakehamLifeTable.Default;
        Assert.InRange(t.LifeExpectancy(Sex.Male, 65m, 2021), 18.2m, 18.4m);
        Assert.InRange(t.LifeExpectancy(Sex.Female, 65m, 2021), 20.7m, 20.9m);
        Assert.True(t.LifeExpectancy(Sex.Female, 65m, 2021) > t.LifeExpectancy(Sex.Male, 65m, 2021));
        Assert.True(t.LifeExpectancy(Sex.Male, 65m, 2040) > t.LifeExpectancy(Sex.Male, 65m, 2021)); // improvements
    }

    [Fact]
    public void Survival_probabilities_are_well_behaved()
    {
        GompertzMakehamLifeTable t = GompertzMakehamLifeTable.Default;
        Assert.Equal(1m, t.SurvivalProbability(Sex.Male, 65m, 0m, 2026));
        Assert.Equal(0m, t.SurvivalProbability(Sex.Male, 65m, 60m, 2026));
        decimal p10 = t.SurvivalProbability(Sex.Male, 65m, 10m, 2026);
        decimal p20 = t.SurvivalProbability(Sex.Male, 65m, 20m, 2026);
        Assert.InRange(p10, 0.75m, 0.90m);
        Assert.True(p20 < p10);
        Assert.Throws<ArgumentOutOfRangeException>(() => t.SurvivalProbability(Sex.Male, -1m, 1m, 2026));
    }

    [Fact]
    public void Level_single_life_annuity_at_65_is_in_market_range()
    {
        // At a 4% valuation rate with 4% expenses a 65-year-old male level annuity rate should be roughly 6–7.5%.
        AnnuityFactorResult r = Pricer.Factor(new AnnuityRequest(Sex.Male, 65m, 0.04m, CalendarYear: 2026));
        Assert.InRange(r.AnnuityRate, 0.060m, 0.078m);
        Assert.Equal(r.Factor * 1.04m, r.PricePerPound);
        Assert.InRange(r.MemberExpectancy, 18m, 20m);
    }

    [Fact]
    public void Escalation_guarantee_and_spouse_each_raise_the_price()
    {
        AnnuityRequest baseline = new(Sex.Male, 65m, 0.04m, CalendarYear: 2026);
        decimal level = Pricer.Factor(baseline).PricePerPound;
        decimal escalating = Pricer.Factor(baseline with { EscalationRate = 0.03m }).PricePerPound;
        decimal guaranteed = Pricer.Factor(baseline with { GuaranteeYears = 10 }).PricePerPound;
        decimal joint = Pricer.Factor(baseline with { SpouseFraction = 0.5m }).PricePerPound;
        Assert.True(escalating > level * 1.25m);
        Assert.True(guaranteed > level);
        Assert.True(joint > level);
        Assert.True(Pricer.Factor(baseline with { Age = 75m }).PricePerPound < level);
        Assert.True(Pricer.Factor(baseline with { Sex = Sex.Female }).PricePerPound > level);
    }

    [Fact]
    public void Higher_interest_lowers_the_price_and_zero_interest_matches_expected_payments()
    {
        AnnuityRequest r = new(Sex.Female, 65m, 0m, ExpenseLoading: 0m, CalendarYear: 2026);
        AnnuityFactorResult zero = Pricer.Factor(r);
        // With no interest and no expenses the factor is the expected number of years paid (monthly in advance ≈ e65 + 1/24).
        Assert.InRange(Math.Abs(zero.Factor - zero.MemberExpectancy), 0m, 0.1m);
        Assert.True(Pricer.Factor(r with { InterestRate = 0.05m }).Factor < zero.Factor);
    }

    [Fact]
    public void Spouse_age_gap_follows_cobs_19_annex_4c()
    {
        AnnuityRequest male = new(Sex.Male, 65m, 0.03m, SpouseFraction: 0.5m, CalendarYear: 2026);
        AnnuityRequest femaleSpouseOlder = male with { SpouseAge = 62m, SpouseSex = Sex.Female };
        Assert.Equal(Pricer.Factor(male).Factor, Pricer.Factor(femaleSpouseOlder).Factor);
        AnnuityRequest female = new(Sex.Female, 65m, 0.03m, SpouseFraction: 0.5m, CalendarYear: 2026);
        Assert.Equal(Pricer.Factor(female).Factor, Pricer.Factor(female with { SpouseAge = 68m, SpouseSex = Sex.Male }).Factor);
    }

    [Fact]
    public void Cost_scales_linearly_and_validates()
    {
        AnnuityRequest r = new(Sex.Male, 67m, 0.035m, CalendarYear: 2026);
        Assert.Equal(Pricer.Cost(10_000m, r) * 2m, Pricer.Cost(20_000m, r));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pricer.Cost(-1m, r));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pricer.Factor(r with { Age = 150m }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pricer.Factor(r with { SpouseFraction = 1.5m }));
    }

    [Fact]
    public void Pricing_is_deterministic()
    {
        AnnuityRequest r = new(Sex.Female, 60m, 0.045m, 0.025m, 5, 0.5m, CalendarYear: 2027);
        Assert.Equal(Pricer.Factor(r).PricePerPound, new AnnuityPricer(GompertzMakehamLifeTable.Default).Factor(r).PricePerPound);
    }
}
