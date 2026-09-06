using FluentValidation;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Application.Validation;

/// <summary>Reusable rule fragments.</summary>
internal static class Rules
{
    public const decimal WeightTolerance = 0.0001m;

    public static IRuleBuilderOptions<T, decimal> Percentage<T>(this IRuleBuilder<T, decimal> rule, decimal min = 0m, decimal max = 100m) =>
        rule.InclusiveBetween(min, max).WithMessage($"Must be a percentage between {min} and {max}.");

    public static IRuleBuilderOptions<T, decimal?> OptionalPercentage<T>(this IRuleBuilder<T, decimal?> rule, decimal min = 0m, decimal max = 100m) =>
        rule.Must(v => v is null || (v >= min && v <= max)).WithMessage($"Must be a percentage between {min} and {max}.");

    public static IRuleBuilderOptions<T, decimal> NonNegativeMoney<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThanOrEqualTo(0m).LessThanOrEqualTo(1_000_000_000m).WithMessage("Must be a non-negative amount.");

    public static bool WeightsSumTo100(IEnumerable<HoldingDto> holdings)
    {
        List<HoldingDto> list = [.. holdings];
        return list.Count == 0 || Math.Abs(list.Sum(h => h.WeightPct) - 100m) <= WeightTolerance;
    }
}

public sealed class ClientWriteValidator : AbstractValidator<ClientWrite>
{
    public ClientWriteValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Title).MaximumLength(20);
        RuleFor(c => c.DateOfBirth).Must(d => d >= new DateOnly(1900, 1, 1) && d <= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Date of birth must be between 1900 and today.");
        RuleFor(c => c.Email).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.AnnualSalary).NonNegativeMoney();
        RuleFor(c => c.TargetRetirementAge).InclusiveBetween(50, 80);
        RuleFor(c => c.RiskProfile).InclusiveBetween(1, 7);
        RuleFor(c => c.StatePension.ForecastWeeklyAmount).Must(v => v is null || (v >= 0m && v <= 1000m)).WithMessage("Weekly State Pension must be between £0 and £1,000.");
        RuleFor(c => c.StatePension.QualifyingYears).Must(v => v is null || (v >= 0 && v <= 60)).WithMessage("Qualifying years must be between 0 and 60.");
        RuleFor(c => c.NationalInsuranceNumber).Matches("^[A-Za-z]{2}[0-9]{6}[A-Za-z]?$").When(c => !string.IsNullOrWhiteSpace(c.NationalInsuranceNumber)).WithMessage("National Insurance number format is invalid.");
    }
}

public sealed class TieredChargeDtoValidator : AbstractValidator<TieredChargeDto>
{
    public TieredChargeDtoValidator()
    {
        RuleFor(t => t.Bands).NotEmpty().WithMessage("At least one band is required.");
        RuleFor(t => t.Bands).Must(b => b.Count == 0 || b[^1].UpTo is null).WithMessage("The last band must be unbounded (upTo empty).");
        RuleFor(t => t.Bands).Must(b => b.Take(Math.Max(0, b.Count - 1)).All(x => x.UpTo is > 0m)).WithMessage("Every band except the last needs a positive upper bound.");
        RuleFor(t => t.Bands).Must(b => b.Count < 2 || b.Take(b.Count - 1).Zip(b.Skip(1).Take(b.Count - 2), (x, y) => x.UpTo < y.UpTo).All(ok => ok)).WithMessage("Band upper bounds must ascend.");
        RuleForEach(t => t.Bands).ChildRules(b => b.RuleFor(x => x.AnnualRatePct).Percentage(0m, 10m));
    }
}

public sealed class ChargeScheduleDtoValidator : AbstractValidator<ChargeScheduleDto>
{
    public ChargeScheduleDtoValidator()
    {
        RuleFor(c => c.PlatformCharge!).SetValidator(new TieredChargeDtoValidator()).When(c => c.PlatformCharge is not null);
        RuleFor(c => c.ProductCharge!).SetValidator(new TieredChargeDtoValidator()).When(c => c.ProductCharge is not null);
        RuleForEach(c => c.FixedCharges).ChildRules(f =>
        {
            f.RuleFor(x => x.Amount).NonNegativeMoney();
            f.RuleFor(x => x.Indexation.RatePct).Percentage(0m, 50m);
        });
        RuleFor(c => c.FundCharge.OcfPct).OptionalPercentage(0m, 10m);
        RuleFor(c => c.FundCharge).Must(f => f.Kind != FundChargeBasisKind.Explicit || f.OcfPct is not null).WithMessage("An explicit fund charge needs ocfPct.");
        RuleFor(c => c.TransactionCostsPct).Percentage(0m, 10m);
        RuleFor(c => c.AdviserCharges.InitialPct).Percentage(0m, 10m);
        RuleFor(c => c.AdviserCharges.OngoingPct).Percentage(0m, 5m);
        RuleFor(c => c.AdviserCharges.InitialAmount).NonNegativeMoney();
        RuleFor(c => c.AdviserCharges.OngoingAmount).NonNegativeMoney();
        RuleFor(c => c.DealingCharges.ExpectedFundDealsPerYear).InclusiveBetween(0, 1000);
        RuleFor(c => c.DealingCharges.ExpectedEtfDealsPerYear).InclusiveBetween(0, 1000);
        RuleForEach(c => c.ExitPenalty.Bands).ChildRules(b =>
        {
            b.RuleFor(x => x.RatePct).Percentage(0m, 100m);
            b.RuleFor(x => x.Amount).NonNegativeMoney();
            b.RuleFor(x => x.UntilYearsFromStart).Must(v => v is null || v > 0m).WithMessage("untilYearsFromStart must be positive or empty.");
        });
        RuleFor(c => c.BidOfferSpreadPct).Percentage(0m, 10m);
        RuleFor(c => c.AllocationRatePct).Percentage(50m, 150m);
        RuleForEach(c => c.LargeFundDiscounts).ChildRules(d =>
        {
            d.RuleFor(x => x.Threshold).GreaterThan(0m);
            d.RuleFor(x => x.RebateRatePct).Percentage(0m, 5m);
        });
    }
}

public sealed class HoldingDtoValidator : AbstractValidator<HoldingDto>
{
    public HoldingDtoValidator()
    {
        RuleFor(h => h.Name).NotEmpty().MaximumLength(200);
        RuleFor(h => h.WeightPct).Percentage(0m, 100m);
        RuleFor(h => h.Isin).Must(i => i is null || Holding.IsValidIsin(i)).WithMessage("ISIN is not valid (check digit failed).");
        RuleFor(h => h.OcfPct).OptionalPercentage(0m, 10m);
    }
}

public sealed class IndexRuleDtoValidator : AbstractValidator<IndexRuleDto>
{
    public IndexRuleDtoValidator()
    {
        RuleFor(r => r.RatePct).Percentage(0m, 15m);
        RuleFor(r => r.CapPct).OptionalPercentage(0m, 15m);
        RuleFor(r => r.FloorPct).OptionalPercentage(0m, 15m);
        RuleFor(r => r).Must(r => r.CapPct is null || r.FloorPct is null || r.FloorPct <= r.CapPct).WithMessage("The floor cannot exceed the cap.");
    }
}

public sealed class SchemeWriteValidator : AbstractValidator<SchemeWrite>
{
    public SchemeWriteValidator()
    {
        RuleFor(s => s.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(s => s.CurrentValue).NonNegativeMoney();
        RuleFor(s => s.TransferValue).NonNegativeMoney();
        RuleFor(s => s.ValuationDate).Must(d => d >= new DateOnly(1980, 1, 1) && d <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)).WithMessage("Valuation date must be between 1980 and today.");
        RuleFor(s => s.SelectedRetirementAge).Must(a => a is null || (a >= 50 && a <= 80)).WithMessage("Retirement age must be between 50 and 80.");
        RuleFor(s => s.Charges).SetValidator(new ChargeScheduleDtoValidator());
        RuleFor(s => s.Guarantees.GuaranteedAnnuityRatePct).OptionalPercentage(0m, 25m);
        RuleFor(s => s.Guarantees.GuaranteedGrowthRatePct).OptionalPercentage(0m, 15m);
        RuleFor(s => s.Guarantees.ProtectedTaxFreeCashPct).OptionalPercentage(25m, 100m);
        RuleFor(s => s.Guarantees.MarketValueReductionPct).Percentage(0m, 100m);
        RuleForEach(s => s.Contributions).ChildRules(c =>
        {
            c.RuleFor(x => x.Amount).NonNegativeMoney();
            c.RuleFor(x => x.EscalationPct).Percentage(-50m, 50m);
            c.RuleFor(x => x.StartMonth).Must(m => m is null || m >= 1).WithMessage("startMonth must be at least 1.");
            c.RuleFor(x => x).Must(x => x.EndMonth is null || x.StartMonth is null || x.EndMonth >= x.StartMonth).WithMessage("endMonth cannot precede startMonth.");
        });
        RuleForEach(s => s.Holdings).SetValidator(new HoldingDtoValidator());
        RuleFor(s => s.Holdings).Must(Rules.WeightsSumTo100).WithMessage("Holding weights must sum to 100%.");
        RuleFor(s => s.DefinedBenefit).NotNull().When(s => s.Type == SchemeType.DefinedBenefit).WithMessage("Defined benefit details are required for a DB scheme.");
        RuleFor(s => s.DefinedBenefit!).SetValidator(new DefinedBenefitDtoValidator()).When(s => s.DefinedBenefit is not null);
    }
}

public sealed class DefinedBenefitDtoValidator : AbstractValidator<DefinedBenefitDto>
{
    public DefinedBenefitDtoValidator()
    {
        RuleFor(d => d.NormalRetirementAge).InclusiveBetween(50, 75);
        RuleFor(d => d.CetvGuaranteeExpiry).Must(d => d >= new DateOnly(2000, 1, 1)).WithMessage("CETV guarantee expiry is implausible.");
        RuleFor(d => d.Tranches).NotEmpty().WithMessage("At least one tranche is required.");
        RuleForEach(d => d.Tranches).ChildRules(t =>
        {
            t.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            t.RuleFor(x => x.AccruedAnnualPension).NonNegativeMoney();
            t.RuleFor(x => x.Revaluation).SetValidator(new IndexRuleDtoValidator());
            t.RuleFor(x => x.Escalation).SetValidator(new IndexRuleDtoValidator());
        });
        RuleFor(d => d.SpousePensionPct).Percentage(0m, 100m);
        RuleFor(d => d.GuaranteePeriodYears).InclusiveBetween(0, 10);
        RuleFor(d => d.PclsCommutationFactor).InclusiveBetween(0m, 40m);
        RuleFor(d => d.MaxPclsPct).Percentage(0m, 25m);
        RuleFor(d => d.EarlyRetirementReductionPct).Percentage(0m, 10m);
        RuleFor(d => d.EarliestUnreducedAge).Must(a => a is null || (a >= 50 && a <= 75)).WithMessage("Earliest unreduced age must be between 50 and 75.");
        RuleFor(d => d.BridgingPensionAnnual).NonNegativeMoney();
    }
}

public sealed class AdviserChargeDtoValidator : AbstractValidator<AdviserChargeDto>
{
    public AdviserChargeDtoValidator()
    {
        RuleFor(a => a.InitialPct).Percentage(0m, 10m);
        RuleFor(a => a.OngoingPct).Percentage(0m, 5m);
        RuleFor(a => a.InitialAmount).NonNegativeMoney();
        RuleFor(a => a.OngoingAmount).NonNegativeMoney();
    }
}

public sealed class AssumptionOverridesValidator : AbstractValidator<AssumptionOverrides>
{
    public AssumptionOverridesValidator()
    {
        RuleFor(o => o.GrowthLowerPct).OptionalPercentage(-10m, 20m);
        RuleFor(o => o.GrowthIntermediatePct).OptionalPercentage(-10m, 20m);
        RuleFor(o => o.GrowthHigherPct).OptionalPercentage(-10m, 20m);
        RuleFor(o => o.InflationPct).OptionalPercentage(-5m, 15m);
        RuleFor(o => o.EarningsGrowthPct).OptionalPercentage(-5m, 15m);
        RuleFor(o => o.StatePensionIncreasePct).OptionalPercentage(0m, 15m);
    }
}

public sealed class PensionSwitchCalcRequestValidator : AbstractValidator<PensionSwitchCalcRequest>
{
    public PensionSwitchCalcRequestValidator()
    {
        RuleFor(r => r.CedingSchemes).NotEmpty().WithMessage("At least one ceding scheme is required.");
        RuleForEach(r => r.CedingSchemes).Must(c => c.SchemeId is not null || c.Inline is not null).WithMessage("Each ceding scheme needs a schemeId or an inline definition.");
        RuleForEach(r => r.CedingSchemes).ChildRules(c => c.RuleFor(x => x.Inline!).SetValidator(new InlineSchemeValidator()).When(x => x.Inline is not null));
        RuleFor(r => r.ProposedProductId).NotEmpty();
        RuleForEach(r => r.ProposedHoldings).SetValidator(new HoldingDtoValidator());
        RuleFor(r => r.ProposedHoldings).Must(Rules.WeightsSumTo100).WithMessage("Proposed holding weights must sum to 100%.");
        RuleFor(r => r).Must(r => r.ProposedHoldings.Count > 0 || r.ProposedModelPortfolioId is not null).WithMessage("Choose proposed holdings or a model portfolio.");
        RuleFor(r => r.ProposedAdviserCharges).SetValidator(new AdviserChargeDtoValidator());
        RuleFor(r => r.RetirementAge).InclusiveBetween(50, 80);
        RuleFor(r => r.ClientAge).Must(a => a is null || (a >= 16 && a <= 110)).WithMessage("Client age must be between 16 and 110.");
        RuleFor(r => r.Overrides!).SetValidator(new AssumptionOverridesValidator()).When(r => r.Overrides is not null);
    }
}

public sealed class InlineSchemeValidator : AbstractValidator<InlineScheme>
{
    public InlineSchemeValidator()
    {
        Include(new SchemeWriteValidatorForInline());
        RuleFor(s => s.Name).NotEmpty().MaximumLength(200);
    }

    private sealed class SchemeWriteValidatorForInline : AbstractValidator<InlineScheme>
    {
        public SchemeWriteValidatorForInline()
        {
            RuleFor(s => (SchemeWrite)s).SetValidator(new SchemeWriteValidator());
        }
    }
}

public sealed class DbTransferCalcRequestValidator : AbstractValidator<DbTransferCalcRequest>
{
    public DbTransferCalcRequestValidator()
    {
        RuleFor(r => r).Must(r => r.DbSchemeId is not null || r.Inline is not null).WithMessage("A DB scheme id or an inline scheme is required.");
        RuleFor(r => r.Inline!).SetValidator(new InlineSchemeValidator()).When(r => r.Inline is not null);
        RuleFor(r => r.ProposedProductId).NotEmpty();
        RuleForEach(r => r.ProposedHoldings).SetValidator(new HoldingDtoValidator());
        RuleFor(r => r.ProposedHoldings).NotEmpty().Must(Rules.WeightsSumTo100).WithMessage("Proposed holdings are required and must sum to 100%.");
        RuleFor(r => r.ProposedAdviserCharges).SetValidator(new AdviserChargeDtoValidator());
        RuleFor(r => r.AptaGrowthPct).Percentage(-10m, 20m);
        RuleFor(r => r.PlanEndAge).InclusiveBetween(75, 110);
        RuleFor(r => r.InitialAdviceFee).NonNegativeMoney();
        RuleFor(r => r.WorkplaceDefaultChargePct).OptionalPercentage(0m, 5m);
        RuleFor(r => r.Overrides!).SetValidator(new AssumptionOverridesValidator()).When(r => r.Overrides is not null);
    }
}

public sealed class PersonDtoValidator : AbstractValidator<PersonDto>
{
    public PersonDtoValidator()
    {
        RuleFor(p => p.Name).NotEmpty().MaximumLength(100);
        RuleFor(p => p.DateOfBirth).Must(d => d >= new DateOnly(1900, 1, 1) && d <= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Date of birth must be between 1900 and today.");
        RuleFor(p => p.RetirementAge).InclusiveBetween(50, 80);
        RuleFor(p => p.StatePensionForecastWeekly).Must(v => v is null || (v >= 0m && v <= 1000m)).WithMessage("Weekly State Pension must be between £0 and £1,000.");
        RuleFor(p => p.StatePensionQualifyingYears).Must(v => v is null || (v >= 0 && v <= 60)).WithMessage("Qualifying years must be between 0 and 60.");
    }
}

public sealed class CashflowCalcRequestValidator : AbstractValidator<CashflowCalcRequest>
{
    public CashflowCalcRequestValidator()
    {
        RuleFor(r => r.Person).SetValidator(new PersonDtoValidator());
        RuleFor(r => r.Partner!).SetValidator(new PersonDtoValidator()).When(r => r.Partner is not null);
        RuleFor(r => r.PlanEndAge).InclusiveBetween(55, 120);
        RuleForEach(r => r.Incomes).ChildRules(i =>
        {
            i.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            i.RuleFor(x => x.AnnualAmount).NonNegativeMoney();
            i.RuleFor(x => x.FromAge).InclusiveBetween(16, 110);
            i.RuleFor(x => x).Must(x => x.ToAge is null || x.ToAge >= x.FromAge).WithMessage("toAge cannot precede fromAge.");
            i.RuleFor(x => x.GrowthPct).Percentage(-10m, 20m);
            i.RuleFor(x => x.PersonIndex).InclusiveBetween(0, 1);
        });
        RuleForEach(r => r.Expenses).ChildRules(e =>
        {
            e.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            e.RuleFor(x => x.AnnualAmount).NonNegativeMoney();
            e.RuleFor(x => x.FromAge).InclusiveBetween(16, 110);
            e.RuleFor(x => x).Must(x => x.ToAge is null || x.ToAge >= x.FromAge).WithMessage("toAge cannot precede fromAge.");
        });
        RuleForEach(r => r.Assets).ChildRules(a =>
        {
            a.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            a.RuleFor(x => x.Value).NonNegativeMoney();
            a.RuleFor(x => x.GrowthPct).Percentage(-10m, 20m);
            a.RuleFor(x => x.Charges).SetValidator(new ChargeScheduleDtoValidator());
            a.RuleFor(x => x.CostBasis).NonNegativeMoney();
            a.RuleFor(x => x.AnnualContribution).NonNegativeMoney();
            a.RuleFor(x => x.EmployerContribution).NonNegativeMoney();
            a.RuleFor(x => x.PersonIndex).InclusiveBetween(0, 1);
            a.RuleFor(x => x.Allocation).Must(al => al is null || Math.Abs(al.EquityPct + al.FixedInterestPct + al.PropertyPct + al.CashPct + al.AlternativesPct - 100m) <= Rules.WeightTolerance).WithMessage("Asset allocation must sum to 100%.");
        });
        RuleForEach(r => r.Events).ChildRules(e =>
        {
            e.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            e.RuleFor(x => x.AtAge).InclusiveBetween(16, 110);
        });
        RuleFor(r => r.Strategy.WithdrawalOrder).NotEmpty().Must(o => o.Distinct().Count() == o.Count).WithMessage("Withdrawal order must not repeat an asset kind.");
        RuleFor(r => r.Strategy.DrawdownParameter).GreaterThanOrEqualTo(0m);
        RuleFor(r => r.Strategy.AnnuityPurchaseAge).Must(a => a is null || (a >= 55 && a <= 90)).WithMessage("Annuity purchase age must be between 55 and 90.");
        RuleFor(r => r.Paths).Must(p => p is null || (p >= 100 && p <= 10_000)).WithMessage("Paths must be between 100 and 10,000.");
        RuleFor(r => r.Overrides!).SetValidator(new AssumptionOverridesValidator()).When(r => r.Overrides is not null);
    }
}

public sealed class RiyCalcRequestValidator : AbstractValidator<RiyCalcRequest>
{
    public RiyCalcRequestValidator()
    {
        RuleFor(r => r.StartValue).NonNegativeMoney();
        RuleFor(r => r.Months).InclusiveBetween(1, 1200);
        RuleFor(r => r.GrowthPct).Percentage(-50m, 50m);
        RuleFor(r => r.Charges).SetValidator(new ChargeScheduleDtoValidator());
        RuleFor(r => r.WeightedOcfPct).OptionalPercentage(0m, 10m);
        RuleFor(r => r.InflationPct).Percentage(-5m, 15m);
    }
}

public sealed class TaxCalcRequestValidator : AbstractValidator<TaxCalcRequest>
{
    public TaxCalcRequestValidator()
    {
        RuleFor(r => r.EarnedIncome).NonNegativeMoney();
        RuleFor(r => r.PensionIncome).NonNegativeMoney();
        RuleFor(r => r.OtherIncome).NonNegativeMoney();
        RuleFor(r => r.SavingsInterest).NonNegativeMoney();
        RuleFor(r => r.Dividends).NonNegativeMoney();
        RuleFor(r => r.GrossPensionContributions).NonNegativeMoney();
        RuleFor(r => r.ReliefAtSourceContributions).NonNegativeMoney().LessThanOrEqualTo(r => r.GrossPensionContributions).WithMessage("Relief-at-source contributions cannot exceed gross contributions.");
    }
}

public sealed class PensionSwitchAnalysisWriteValidator : AbstractValidator<PensionSwitchAnalysisWrite>
{
    public PensionSwitchAnalysisWriteValidator()
    {
        RuleFor(a => a.ClientId).NotEmpty();
        RuleFor(a => a.Title).NotEmpty().MaximumLength(200);
        RuleFor(a => a.RetirementAge).InclusiveBetween(50, 80);
        RuleFor(a => a.AssumptionSetId).NotEmpty();
        RuleForEach(a => a.ProposedHoldings).SetValidator(new HoldingDtoValidator());
        RuleFor(a => a.ProposedHoldings).Must(Rules.WeightsSumTo100).WithMessage("Proposed holding weights must sum to 100%.");
        RuleFor(a => a.ProposedAdviserCharges).SetValidator(new AdviserChargeDtoValidator());
        RuleFor(a => a.Rationale).MaximumLength(4000);
        RuleFor(a => a.Overrides!).SetValidator(new AssumptionOverridesValidator()).When(a => a.Overrides is not null);
    }
}

public sealed class DbTransferAnalysisWriteValidator : AbstractValidator<DbTransferAnalysisWrite>
{
    public DbTransferAnalysisWriteValidator()
    {
        RuleFor(a => a.ClientId).NotEmpty();
        RuleFor(a => a.DbSchemeId).NotEmpty();
        RuleFor(a => a.AssumptionSetId).NotEmpty();
        RuleFor(a => a.PlanEndAge).InclusiveBetween(75, 110);
        RuleForEach(a => a.ProposedHoldings).SetValidator(new HoldingDtoValidator());
        RuleFor(a => a.ProposedHoldings).Must(Rules.WeightsSumTo100).WithMessage("Proposed holding weights must sum to 100%.");
        RuleFor(a => a.ProposedAdviserCharges).SetValidator(new AdviserChargeDtoValidator());
        RuleFor(a => a.AptaGrowthPct).OptionalPercentage(-10m, 20m);
        RuleFor(a => a.InitialAdviceFee).NonNegativeMoney();
        RuleFor(a => a.WorkplaceDefaultChargePct).OptionalPercentage(0m, 5m);
        RuleFor(a => a.ContingentChargingCarveOut).NotEmpty().When(a => a.ChargeBasis == Domain.Analysis.AdviserChargeBasis.Contingent).WithMessage("Contingent charging requires a recorded COBS 19.1B.9R carve-out.");
        RuleFor(a => a.Overrides!).SetValidator(new AssumptionOverridesValidator()).When(a => a.Overrides is not null);
    }
}

public sealed class CashflowPlanWriteValidator : AbstractValidator<CashflowPlanWrite>
{
    public CashflowPlanWriteValidator()
    {
        RuleFor(p => p.ClientId).NotEmpty();
        RuleFor(p => p.Title).NotEmpty().MaximumLength(200);
        RuleFor(p => p.AssumptionSetId).NotEmpty();
        RuleFor(p => p.PlanEndAge).InclusiveBetween(60, 110);
        RuleFor(p => p.StochasticPaths).InclusiveBetween(100, 10_000);
        RuleFor(p => p).Must(p => p.Strategy.WithdrawalOrder.Count > 0 && p.Strategy.WithdrawalOrder.Distinct().Count() == p.Strategy.WithdrawalOrder.Count).WithMessage("Withdrawal order must be non-empty without repeats.");
        RuleForEach(p => p.Assets).ChildRules(a =>
        {
            a.RuleFor(x => x.Name).NotEmpty();
            a.RuleFor(x => x.Value).NonNegativeMoney();
            a.RuleFor(x => x.GrowthPct).Percentage(-10m, 20m);
            a.RuleFor(x => x.Charges).SetValidator(new ChargeScheduleDtoValidator());
        });
        RuleForEach(p => p.Incomes).ChildRules(i =>
        {
            i.RuleFor(x => x.Name).NotEmpty();
            i.RuleFor(x => x.AnnualAmount).NonNegativeMoney();
            i.RuleFor(x => x.FromAge).InclusiveBetween(16, 110);
            i.RuleFor(x => x.GrowthPct).Percentage(-10m, 20m);
        });
        RuleForEach(p => p.Expenses).ChildRules(e =>
        {
            e.RuleFor(x => x.Name).NotEmpty();
            e.RuleFor(x => x.AnnualAmount).NonNegativeMoney();
            e.RuleFor(x => x.FromAge).InclusiveBetween(16, 110);
        });
        RuleFor(p => p.Overrides!).SetValidator(new AssumptionOverridesValidator()).When(p => p.Overrides is not null);
    }
}

public sealed class AssumptionSetWriteValidator : AbstractValidator<AssumptionSetWrite>
{
    public AssumptionSetWriteValidator()
    {
        RuleFor(a => a.Name).NotEmpty().MaximumLength(100);
        RuleFor(a => a.GrowthLowerPct).Percentage(-10m, 20m);
        RuleFor(a => a.GrowthIntermediatePct).Percentage(-10m, 20m);
        RuleFor(a => a.GrowthHigherPct).Percentage(-10m, 20m);
        RuleFor(a => a).Must(a => a.GrowthLowerPct <= a.GrowthIntermediatePct && a.GrowthIntermediatePct <= a.GrowthHigherPct).WithMessage("Growth rates must be ordered lower ≤ intermediate ≤ higher.");
        RuleFor(a => a.InflationPct).Percentage(-5m, 15m);
        RuleFor(a => a.EarningsGrowthPct).Percentage(-5m, 15m);
        RuleFor(a => a.RpiInflationPct).Percentage(-5m, 15m);
        RuleFor(a => a.ChargeInflationPct).Percentage(0m, 15m);
        RuleFor(a => a.PreRetirementProductChargePct).Percentage(0m, 5m);
        RuleFor(a => a.AnnuityExpenseLoadingPct).Percentage(0m, 20m);
        RuleFor(a => a.SpouseAgeGapYears).InclusiveBetween(0, 10);
        RuleFor(a => a.StatePensionIncreasePct).Percentage(0m, 15m);
        RuleFor(a => a.TaxYear).Matches("^[0-9]{4}/[0-9]{2}$").WithMessage("Tax year must look like 2026/27.");
        RuleFor(a => a.MarketInputs.GiltYieldUpTo5Pct).Percentage(-5m, 20m);
        RuleFor(a => a.MarketInputs.GiltYield5To10Pct).Percentage(-5m, 20m);
        RuleFor(a => a.MarketInputs.GiltYield10To15Pct).Percentage(-5m, 20m);
        RuleFor(a => a.MarketInputs.GiltYieldOver15Pct).Percentage(-5m, 20m);
        RuleFor(a => a.MarketInputs.TvcAnnuityRateRpiLinkedPct).Percentage(-5m, 20m);
        RuleFor(a => a.MarketInputs.TvcAnnuityRateLevelPct).Percentage(-5m, 20m);
        RuleFor(a => a.MarketInputs.Cobs13YPct).Percentage(-5m, 20m);
    }
}

public sealed class CreateReportRequestValidator : AbstractValidator<CreateReportRequest>
{
    public CreateReportRequestValidator()
    {
        RuleFor(r => r.AnalysisId).NotEmpty();
        RuleFor(r => r.Kind).IsInEnum();
        RuleFor(r => r.Format).IsInEnum();
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress();
        RuleFor(r => r.Password).NotEmpty().MinimumLength(8);
    }
}
