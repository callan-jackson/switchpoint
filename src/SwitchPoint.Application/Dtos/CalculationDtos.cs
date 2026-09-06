using SwitchPoint.Calculation.CriticalYield;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Clients;

namespace SwitchPoint.Application.Dtos;

// --- Pension switch ---

/// <summary>A ceding scheme reference: either a stored scheme id or an inline definition.</summary>
public sealed record CedingSchemeRef(Guid? SchemeId, InlineScheme? Inline);

public sealed record InlineScheme : SchemeWrite
{
    public required string Name { get; init; }
}

public sealed record PensionSwitchCalcRequest
{
    public Guid? ClientId { get; init; }
    public required IReadOnlyList<CedingSchemeRef> CedingSchemes { get; init; }
    public required Guid ProposedProductId { get; init; }
    public int? ProposedProductChargeVersion { get; init; }
    public IReadOnlyList<HoldingDto> ProposedHoldings { get; init; } = [];
    public Guid? ProposedModelPortfolioId { get; init; }
    public AdviserChargeDto ProposedAdviserCharges { get; init; } = AdviserChargeDto.None;
    public required int RetirementAge { get; init; }
    public Guid? AssumptionSetId { get; init; }
    public AssumptionOverrides? Overrides { get; init; }
    public bool RedirectContributions { get; init; } = true;

    /// <summary>Client age used when no client id is given (inline schemes); default 55.</summary>
    public int? ClientAge { get; init; }
}

public sealed record CriticalYieldAtRateDto(decimal GrowthPct, decimal ExistingValueAtRetirement, decimal ReceivingValueAtRetirement, decimal CriticalYieldPct, decimal CriticalYieldRealPct, decimal HeadroomPct, decimal ProjectedGain, int? BreakEvenYear, bool Converged);

public sealed record EffectOfChargesRowDto(int Year, decimal PaymentsToDate, decimal BeforeCharges, decimal PlanAndInvestmentChargesOnly, decimal AfterAllCharges, decimal EffectOfDeductionsToDate);

public sealed record ChargeTotalsDto(decimal Platform, decimal Product, decimal Fund, decimal Transaction, decimal AdviserInitial, decimal AdviserOngoing, decimal Fixed, decimal Dealing, decimal Switch, decimal Discounts, decimal AllocationAndSpread, decimal ExitPenalty, decimal Total);

public sealed record RiyDto(
    decimal GrowthPct,
    decimal ProductRiyPct,
    decimal TotalRiyPct,
    decimal RateAfterProductChargesPct,
    decimal RateAfterAllChargesPct,
    decimal ValueBeforeCharges,
    decimal ValueAfterProductCharges,
    decimal ValueAfterAllCharges,
    string ProductSentence,
    string TotalSentence,
    IReadOnlyList<EffectOfChargesRowDto> EffectOfCharges,
    ChargeTotalsDto TotalCharges);

public sealed record CedingSchemeResultDto(string Name, decimal CurrentValue, decimal NetTransferValue, decimal ProjectedValueIfRetained, RiyDto RiyIfRetained, RiyDto RiyIfSwitched, decimal CriticalYieldAlonePct, bool GuaranteesFlagged, SwitchVerdict Verdict);

public sealed record ChartPointDto(int Year, decimal ExistingValue, decimal ReceivingValue);

public sealed record AssumptionSetRefDto(Guid Id, string Name, int Version);

public sealed record PensionSwitchResultDto(
    decimal TotalNetTransferValue,
    decimal InitialAdviserCharge,
    bool AnyGuaranteesFlagged,
    IReadOnlyList<string> Warnings,
    CriticalYieldAtRateDto Lower,
    CriticalYieldAtRateDto Intermediate,
    CriticalYieldAtRateDto Higher,
    RiyDto ReceivingRiy,
    IReadOnlyList<CedingSchemeResultDto> Schemes,
    IReadOnlyList<ChartPointDto> Chart,
    string EngineVersion,
    DateTime CalculatedAtUtc,
    AssumptionSetRefDto AssumptionSet);

public sealed record RiyCalcRequest(decimal StartValue, int Months, decimal GrowthPct, ChargeScheduleDto Charges, decimal? WeightedOcfPct, IReadOnlyList<ContributionDto> Contributions, decimal InflationPct = 2.0m);

// --- DB transfer ---

public sealed record DbTransferCalcRequest
{
    public Guid? ClientId { get; init; }
    public Guid? DbSchemeId { get; init; }
    public InlineScheme? Inline { get; init; }
    public Sex? ClientSex { get; init; }
    public DateOnly? ClientDateOfBirth { get; init; }
    public required Guid ProposedProductId { get; init; }
    public int? ProposedProductChargeVersion { get; init; }
    public IReadOnlyList<HoldingDto> ProposedHoldings { get; init; } = [];
    public AdviserChargeDto ProposedAdviserCharges { get; init; } = AdviserChargeDto.None;
    public required decimal AptaGrowthPct { get; init; }
    public int PlanEndAge { get; init; } = 100;
    public decimal InitialAdviceFee { get; init; }
    public decimal? WorkplaceDefaultChargePct { get; init; }
    public Guid? AssumptionSetId { get; init; }
    public AssumptionOverrides? Overrides { get; init; }
    public DateOnly? TransferDate { get; init; }
}

public sealed record RevaluedTrancheDto(string Name, decimal AccruedAnnualPension, decimal RevaluationRatePct, int YearsRevalued, decimal PensionAtRetirement, decimal EscalationInPaymentPct, decimal AnnuityInterestRatePct, decimal AnnuityPricePerPound, decimal AnnuityCost, bool IsGmp);

public sealed record TvcDto(decimal CashEquivalentTransferValue, decimal EstimatedReplacementCost, decimal Difference, int RetirementAgeUsed, decimal TermYears, decimal GiltYieldUsedPct, decimal DiscountRateUsedPct, decimal AnnuityCostAtRetirement, decimal PensionAtRetirement, string Wording, IReadOnlyList<string> Notes, IReadOnlyList<RevaluedTrancheDto> Tranches);

public sealed record CriticalYieldsDto(decimal TypeAAnnuityMatchPct, decimal TypeBPclsAndReducedPensionPct, decimal DrawdownHurdleRatePct, decimal SchemePcls, decimal ResidualPensionAfterPcls, bool Converged);

public sealed record IncomeComparisonRowDto(int Age, decimal SchemeIncomeNominal, decimal SchemeIncomeReal, decimal DrawdownIncomeReal, decimal ResidualFundReal, decimal SchemeDeathBenefitReal);

public sealed record StressScenarioDto(string Name, decimal SustainableRealIncome, decimal Change);

public sealed record OnePageSummaryDto(decimal InitialAdviceFee, decimal RevaluedMonthlyIncome, int PaybackMonths, decimal FirstYearChargesProposed, decimal OngoingAnnualChargesProposed, decimal FirstYearChargesCeding, decimal? FirstYearChargesWorkplaceDefault);

public sealed record DbTransferResultDto(
    TvcDto Tvc,
    CriticalYieldsDto CriticalYields,
    decimal SustainableRealIncomeFromTransfer,
    IReadOnlyList<IncomeComparisonRowDto> IncomeComparison,
    IReadOnlyList<StressScenarioDto> StressTests,
    OnePageSummaryDto Summary,
    decimal LifeExpectancyAtRetirement,
    IReadOnlyList<string> Warnings,
    string EngineVersion,
    DateTime CalculatedAtUtc,
    AssumptionSetRefDto AssumptionSet);

// --- Cashflow ---

public sealed record PersonDto(string Name, DateOnly DateOfBirth, Sex Sex, TaxRegime TaxRegime, int RetirementAge, decimal? StatePensionForecastWeekly, int? StatePensionQualifyingYears, bool MpaaTriggered);

public sealed record PlanIncomeDto(string Name, IncomeKind Kind, decimal AnnualAmount, int FromAge, int? ToAge, decimal GrowthPct, bool IsTaxable, int PersonIndex);

public sealed record PlanExpenseDto(string Name, decimal AnnualAmount, int FromAge, int? ToAge);

public sealed record PlanAssetDto(string Name, PlanAssetKind Kind, decimal Value, decimal GrowthPct, ChargeScheduleDto Charges, Guid? SchemeId, decimal CostBasis, decimal AnnualContribution, decimal EmployerContribution, bool SalarySacrifice, int PersonIndex, AssetAllocationDto? Allocation);

public sealed record PlanEventDto(string Name, int AtAge, decimal Amount);

public sealed record PlanStrategyDto(IReadOnlyList<PlanAssetKind> WithdrawalOrder, CrystallisationChoice Crystallisation, WithdrawalRule DrawdownRule, decimal DrawdownParameter, bool ReinvestSurplusIntoIsa, int? AnnuityPurchaseAge)
{
    public static PlanStrategyDto Default { get; } = new(PlanStrategy.Default.WithdrawalOrder, PlanStrategy.Default.Crystallisation, PlanStrategy.Default.DrawdownRule, 0m, true, null);
}

public sealed record CashflowCalcRequest
{
    public Guid? ClientId { get; init; }
    public required PersonDto Person { get; init; }
    public PersonDto? Partner { get; init; }
    public int PlanEndAge { get; init; } = 100;
    public IReadOnlyList<PlanIncomeDto> Incomes { get; init; } = [];
    public IReadOnlyList<PlanExpenseDto> Expenses { get; init; } = [];
    public IReadOnlyList<PlanAssetDto> Assets { get; init; } = [];
    public IReadOnlyList<PlanEventDto> Events { get; init; } = [];
    public PlanStrategyDto Strategy { get; init; } = PlanStrategyDto.Default;
    public Guid? AssumptionSetId { get; init; }
    public AssumptionOverrides? Overrides { get; init; }
    public ulong? Seed { get; init; }
    public int? Paths { get; init; }
}

public sealed record AssetValueDto(string Name, PlanAssetKind Kind, decimal Value, decimal ValueReal);

public sealed record CashflowRowDto(
    int Year, int Age, int? PartnerAge,
    decimal EmploymentIncome, decimal StatePensionIncome, decimal DbPensionIncome, decimal OtherIncome,
    decimal PensionWithdrawalsTaxable, decimal TaxFreeCash, decimal IsaWithdrawals, decimal GiaWithdrawals, decimal CashWithdrawals,
    decimal IncomeTax, decimal NationalInsurance, decimal CapitalGainsTax, decimal NetIncome, decimal NetIncomeReal,
    decimal Expenses, decimal Surplus, decimal Shortfall, decimal Contributions,
    IReadOnlyList<AssetValueDto> Assets, decimal TotalAssets, decimal TotalAssetsReal, IReadOnlyList<string> Warnings);

public sealed record CashflowResultDto(
    IReadOnlyList<CashflowRowDto> Rows,
    int? FirstShortfallAge,
    decimal TotalIncomeTax,
    decimal TotalShortfall,
    decimal LegacyAtEnd,
    decimal LegacyAtEndReal,
    decimal LumpSumAllowanceUsed,
    bool Succeeds,
    decimal SustainableSpend,
    string EngineVersion,
    DateTime CalculatedAtUtc,
    AssumptionSetRefDto AssumptionSet);

public sealed record PercentileRowDto(int Year, int Age, decimal P5, decimal P10, decimal P25, decimal P50, decimal P75, decimal P90, decimal P95);

public sealed record ConservativenessDto(decimal DeterministicAssetsAtEnd, decimal MedianAssetsAtEnd, bool MedianIsNoLessConservative);

public sealed record StochasticResultDto(
    string Seed,
    int Paths,
    decimal ProbabilityOfSuccess,
    IReadOnlyList<PercentileRowDto> TotalAssetsReal,
    IReadOnlyList<PercentileRowDto> NetIncomeReal,
    int? MedianShortfallAge,
    int? WorstDecileShortfallAge,
    ConservativenessDto Conservativeness,
    decimal MeanLegacyReal,
    string EngineVersion,
    DateTime CalculatedAtUtc);

// --- Tax ---

public sealed record TaxCalcRequest(string? TaxYear, TaxRegime Regime, decimal EarnedIncome, decimal PensionIncome, decimal OtherIncome, decimal SavingsInterest, decimal Dividends, decimal GrossPensionContributions, decimal ReliefAtSourceContributions, bool SubjectToNi);

public sealed record TaxLineDto(string Category, string Band, decimal Amount, decimal RatePct, decimal Tax);

public sealed record TaxComputationDto(string TaxYear, TaxRegime Regime, decimal AdjustedNetIncome, decimal PersonalAllowance, decimal TaxableIncome, decimal IncomeTax, decimal NationalInsurance, decimal TotalDeductions, decimal MarginalRatePct, IReadOnlyList<TaxLineDto> Lines);
