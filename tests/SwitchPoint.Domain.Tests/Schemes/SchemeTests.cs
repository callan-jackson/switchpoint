using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Domain.Tests.Schemes;

public class SchemeTests
{
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("GB00B3X7QG63", true)]   // Vanguard FTSE UK All Share Index Unit Trust Acc
    [InlineData("gb00b3x7qg63", true)]
    [InlineData("IE00B4L5Y983", true)]   // iShares Core MSCI World
    [InlineData("US0378331005", true)]   // Apple
    [InlineData("GB00B3X7QG64", false)]  // bad check digit
    [InlineData("GB00B3X7QG6", false)]
    [InlineData("1234567890AB", false)]
    [InlineData("", false)]
    public void Isin_validation_uses_luhn(string isin, bool valid) => Assert.Equal(valid, Holding.IsValidIsin(isin));

    [Fact]
    public void Holding_rejects_invalid_isin() =>
        Assert.Throws<DomainException>(() => new Holding("Fund", 1m, "GB00B3X7QG64"));

    [Fact]
    public void Weighted_ocf_uses_holding_or_catalogue_values()
    {
        Holding[] h = [new Holding("A", 0.5m, ocf: 0.0022m), new Holding("B", 0.5m, "IE00B4L5Y983")];
        Assert.Equal(0.0021m, Holding.WeightedOcf(h, x => x.Isin == "IE00B4L5Y983" ? 0.0020m : null));
        Assert.Throws<DomainException>(() => Holding.WeightedOcf(h));
    }

    [Fact]
    public void Holdings_must_sum_to_one()
    {
        Scheme s = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SchemeType.PersonalPension, "Old PP", 80_000m, 78_000m, new DateOnly(2026, 9, 1), Now);
        Assert.Throws<DomainException>(() => s.ReplaceHoldings([new Holding("A", 0.5m)], Now));
        s.ReplaceHoldings([new Holding("A", 0.5m), new Holding("B", 0.5m)], Now);
        Assert.Equal(2, s.Holdings.Count);
    }

    [Fact]
    public void Net_transfer_value_applies_exit_penalty_by_years_in_force()
    {
        Scheme s = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SchemeType.PersonalPension, "Legacy PP", 100_000m, 100_000m, new DateOnly(2026, 9, 1), Now);
        s.SetProduct(null, "Legacy PP", "P123", new DateOnly(2024, 9, 1), Now);
        s.SetCharges(new ChargeSchedule { ExitPenalty = ExitPenaltySchedule.Declining((3m, 0.05m), (5m, 0.02m)) }, Now);
        Assert.Equal(95_000m, s.NetTransferValue(new DateOnly(2026, 9, 1)));  // 2 years in
        Assert.Equal(98_000m, s.NetTransferValue(new DateOnly(2028, 9, 1)));  // 4 years in
        Assert.Equal(100_000m, s.NetTransferValue(new DateOnly(2030, 9, 1))); // expired
        Assert.Equal(0m, new Scheme(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SchemeType.Sipp, "X", 1m, 1m, new DateOnly(2026, 1, 1), Now).YearsInForce(new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void Guarantees_flag_referral()
    {
        Assert.False(Guarantees.None.RequiresReferral);
        Assert.True(new Guarantees(guaranteedAnnuityRate: 0.09m).RequiresReferral);
        Assert.True(new Guarantees(withProfits: true).RequiresReferral);
        Assert.True(new Guarantees(protectedTaxFreeCashFraction: 0.4m).RequiresReferral);
        Assert.False(new Guarantees(protectedTaxFreeCashFraction: 0.25m).RequiresReferral);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Guarantees(guaranteedAnnuityRate: 0.5m));
    }

    [Fact]
    public void Revaluation_presets_resolve_with_caps_and_floors()
    {
        Assert.Equal(0.02m, RevaluationRule.StatutoryPost2009.AnnualRate(0.02m, 0.03m, 0.035m));
        Assert.Equal(0.025m, RevaluationRule.StatutoryPost2009.AnnualRate(0.03m, 0.035m, 0.04m));
        Assert.Equal(0.03m, RevaluationRule.StatutoryPre2009.AnnualRate(0.03m, 0.035m, 0.04m));
        Assert.Equal(0.035m, RevaluationRule.Rpi().AnnualRate(0.02m, 0.035m, 0.04m));
        Assert.Equal(0.04m, RevaluationRule.Section148.AnnualRate(0.02m, 0.035m, 0.04m));
        Assert.Equal(0.0625m, RevaluationRule.Fixed(0.0625m).AnnualRate(0m, 0m, 0m));
        Assert.Equal(0m, RevaluationRule.None.AnnualRate(0.02m, 0.03m, 0.035m));
        Assert.Equal(0.03m, RevaluationRule.Cpi(cap: 0.05m, floor: 0.03m).AnnualRate(0.02m, 0.03m, 0.035m));
        Assert.Throws<DomainException>(() => RevaluationRule.Cpi(cap: 0.02m, floor: 0.03m));
    }

    [Fact]
    public void Escalation_presets_resolve()
    {
        Assert.Equal(0.02m, EscalationRule.StatutoryPost2005.AnnualRate(0.02m, 0.03m));
        Assert.Equal(0.025m, EscalationRule.StatutoryPost2005.AnnualRate(0.04m, 0.05m));
        Assert.Equal(0.05m, EscalationRule.StatutoryPre2005.AnnualRate(0.06m, 0.07m));
        Assert.Equal(0.03m, EscalationRule.Fixed(0.03m).AnnualRate(0m, 0m));
        Assert.Equal(0m, EscalationRule.None.AnnualRate(0.02m, 0.03m));
    }

    [Fact]
    public void Defined_benefit_scheme_validates_terms()
    {
        DefinedBenefitScheme db = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ABC Pension Scheme", new DateOnly(2015, 3, 31), 65, 450_000m, new DateOnly(2026, 8, 1), new DateOnly(2026, 11, 1), Now);
        Assert.Equal(450_000m, db.CashEquivalentTransferValue);
        Assert.Throws<DomainException>(() => db.ReplaceTranches([], Now));
        db.ReplaceTranches([
            new DbTranche("Pre-97 GMP", 2_000m, RevaluationRule.Fixed(0.035m), EscalationRule.None, isGmp: true),
            new DbTranche("Post-97", 10_000m, RevaluationRule.StatutoryPre2009, EscalationRule.StatutoryPre2005),
        ], Now);
        Assert.Equal(12_000m, db.TotalAccruedPension);
        Assert.Throws<DomainException>(() => db.UpdateCetv(460_000m, new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 1), Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => db.SetBenefitTerms(0.5m, 5, 20m, 0.25m, 0.04m, 70, 0m, SchemeFundingStatus.FullyFunded, Now));
        db.SetBenefitTerms(0.5m, 5, 18m, 0.25m, 0.05m, 60, 3_000m, SchemeFundingStatus.Deficit, Now);
        Assert.Equal(60, db.EarliestUnreducedAge);
        Assert.True(SchemeType.DefinedBenefit.HasSafeguardedBenefits());
        Assert.True(SchemeType.Sipp.IsPension());
        Assert.False(SchemeType.Isa.IsPension());
    }

    [Fact]
    public void Contribution_grossing_and_ranges()
    {
        Contribution c = new(ContributionPayer.Member, 200m, Frequency.Monthly, 0.03m, isGrossOfTaxRelief: false);
        Assert.Equal(2400m, c.AnnualisedAmount);
        Assert.Throws<DomainException>(() => new Contribution(ContributionPayer.Member, 100m, Frequency.Monthly, startMonth: 12, endMonth: 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Contribution(ContributionPayer.Member, 100m, Frequency.Monthly, escalationRate: 0.9m));
    }
}
