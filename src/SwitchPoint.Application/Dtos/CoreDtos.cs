using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;
using SwitchPoint.Domain.Tenancy;

namespace SwitchPoint.Application.Dtos;

// Percent convention: properties ending in "Pct" are percentages (0.25 = 0.25%); the domain uses fractions.

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record UserDto(Guid Id, string DisplayName, string Email, UserRole Role, Guid FirmId, string FirmName);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);

// --- Clients ---

public sealed record AddressDto(string? Line1, string? Line2, string? Town, string? County, string? Postcode, string? Country);

public sealed record StatePensionDto(decimal? ForecastWeeklyAmount, int? QualifyingYears);

public sealed record ExternalReferenceDto(ExternalSource Source, string? ExternalId);

public sealed record ClientSummary(Guid Id, string FullName, DateOnly DateOfBirth, int Age, string? Email, int RiskProfile, int SchemeCount, decimal TotalPensionValue, DateTime UpdatedAtUtc);

public record ClientWrite
{
    public string? Title { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public Sex Sex { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public AddressDto? Address { get; init; }
    public MaritalStatus MaritalStatus { get; init; } = MaritalStatus.Single;
    public EmploymentStatus EmploymentStatus { get; init; } = EmploymentStatus.Employed;
    public decimal AnnualSalary { get; init; }
    public int TargetRetirementAge { get; init; } = 67;
    public TaxRegime TaxRegime { get; init; } = TaxRegime.RestOfUk;
    public int RiskProfile { get; init; } = 4;
    public HealthStatus Health { get; init; } = HealthStatus.Standard;
    public bool IsSmoker { get; init; }
    public StatePensionDto StatePension { get; init; } = new(null, null);

    /// <summary>Write-only; stored masked.</summary>
    public string? NationalInsuranceNumber { get; init; }
}

public sealed record ClientDetail : ClientWrite
{
    public required Guid Id { get; init; }
    public required string FullName { get; init; }
    public required int Age { get; init; }
    public string? NationalInsuranceNumberMasked { get; init; }
    public required ExternalReferenceDto ExternalReference { get; init; }
    public IReadOnlyList<SchemeDto> Schemes { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

// --- Charges ---

public sealed record TierBandDto(decimal? UpTo, decimal AnnualRatePct);

public sealed record TieredChargeDto(TieredChargeMode Mode, IReadOnlyList<TierBandDto> Bands);

public sealed record IndexationDto(IndexationBasis Basis, decimal RatePct);

public sealed record FixedChargeDto(decimal Amount, Frequency Frequency, IndexationDto Indexation, FixedChargeScope AppliesTo, string? Description);

public sealed record FundChargeDto(FundChargeBasisKind Kind, decimal? OcfPct);

public sealed record AdviserChargeDto(decimal InitialPct, decimal InitialAmount, decimal OngoingPct, decimal OngoingAmount)
{
    public static AdviserChargeDto None { get; } = new(0m, 0m, 0m, 0m);
}

public sealed record DealingChargesDto(decimal FundDealAmount, decimal EtfDealAmount, int ExpectedFundDealsPerYear, int ExpectedEtfDealsPerYear);

public sealed record SwitchChargeDto(decimal AmountPerSwitch, int ExpectedSwitchesPerYear);

public sealed record ExitPenaltyBandDto(decimal? UntilYearsFromStart, decimal RatePct, decimal Amount);

public sealed record ExitPenaltyDto(IReadOnlyList<ExitPenaltyBandDto> Bands);

public sealed record LargeFundDiscountDto(decimal Threshold, decimal RebateRatePct);

public sealed record ChargeScheduleDto
{
    public TieredChargeDto? PlatformCharge { get; init; }
    public TieredChargeDto? ProductCharge { get; init; }
    public IReadOnlyList<FixedChargeDto> FixedCharges { get; init; } = [];
    public FundChargeDto FundCharge { get; init; } = new(FundChargeBasisKind.None, null);
    public decimal TransactionCostsPct { get; init; }
    public AdviserChargeDto AdviserCharges { get; init; } = AdviserChargeDto.None;
    public DealingChargesDto DealingCharges { get; init; } = new(0m, 0m, 0, 0);
    public SwitchChargeDto SwitchCharge { get; init; } = new(0m, 0);
    public ExitPenaltyDto ExitPenalty { get; init; } = new([]);
    public decimal BidOfferSpreadPct { get; init; }
    public decimal AllocationRatePct { get; init; } = 100m;
    public IReadOnlyList<LargeFundDiscountDto> LargeFundDiscounts { get; init; } = [];

    public static ChargeScheduleDto None { get; } = new();
}

// --- Schemes ---

public sealed record ContributionDto(ContributionPayer Payer, decimal Amount, Frequency Frequency, decimal EscalationPct, bool IsGrossOfTaxRelief, int? StartMonth, int? EndMonth);

public sealed record HoldingDto(string Name, decimal WeightPct, string? Isin, Guid? FundId, decimal? OcfPct);

public sealed record GuaranteesDto(decimal? GuaranteedAnnuityRatePct, decimal? GuaranteedGrowthRatePct, decimal? ProtectedTaxFreeCashPct, int? ProtectedPensionAge, bool WithProfits, decimal MarketValueReductionPct, decimal TerminalBonus, decimal LoyaltyBonusPct)
{
    public static GuaranteesDto None { get; } = new(null, null, null, null, false, 0m, 0m, 0m);
}

public sealed record IndexRuleDto(IndexBasis Basis, decimal RatePct, decimal? CapPct, decimal? FloorPct);

public sealed record DbTrancheDto(string Name, decimal AccruedAnnualPension, IndexRuleDto Revaluation, IndexRuleDto Escalation, bool IsGmp);

public sealed record DefinedBenefitDto(
    DateOnly DateOfLeaving,
    int NormalRetirementAge,
    DateOnly CetvGuaranteeExpiry,
    IReadOnlyList<DbTrancheDto> Tranches,
    decimal SpousePensionPct,
    int GuaranteePeriodYears,
    decimal PclsCommutationFactor,
    decimal MaxPclsPct,
    decimal EarlyRetirementReductionPct,
    int? EarliestUnreducedAge,
    decimal BridgingPensionAnnual,
    SchemeFundingStatus FundingStatus);

public record SchemeWrite
{
    public required SchemeType Type { get; init; }
    public Guid? ProviderId { get; init; }
    public required string ProductName { get; init; }
    public string? PolicyNumber { get; init; }
    public decimal CurrentValue { get; init; }
    public decimal TransferValue { get; init; }
    public DateOnly ValuationDate { get; init; }
    public DateOnly? StartDate { get; init; }
    public ChargeScheduleDto Charges { get; init; } = ChargeScheduleDto.None;
    public GuaranteesDto Guarantees { get; init; } = GuaranteesDto.None;
    public int? SelectedRetirementAge { get; init; }
    public bool InDrawdown { get; init; }
    public IReadOnlyList<ContributionDto> Contributions { get; init; } = [];
    public IReadOnlyList<HoldingDto> Holdings { get; init; } = [];
    public DefinedBenefitDto? DefinedBenefit { get; init; }
}

public sealed record SchemeDto : SchemeWrite
{
    public required Guid Id { get; init; }
    public required Guid ClientId { get; init; }
    public string? ProviderName { get; init; }
    public decimal NetTransferValue { get; init; }
    public decimal? WeightedOcfPct { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

// --- Market ---

public sealed record ProviderDto(Guid Id, string Name, ProviderKind Kind, string? FcaFirmReferenceNumber, string? Website);

public sealed record ChargeVersionDto(int Version, DateOnly EffectiveFrom, DateOnly? EffectiveTo, DateOnly AsAt, string? SourceUrl, DataQuality DataQuality, ChargeScheduleDto Charges);

public record ProductSummary
{
    public required Guid Id { get; init; }
    public required Guid ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> WrapperTypes { get; init; } = [];
    public decimal MinimumInvestment { get; init; }
    public bool AllowsFamilyLinking { get; init; }
    public FundUniverse FundUniverse { get; init; }
    public DataQuality DataQuality { get; init; }
    public decimal? EffectiveChargePctAt100k { get; init; }
    public decimal? EffectiveChargePctAt500k { get; init; }
    public int? CurrentChargeVersion { get; init; }
    public DateOnly? AsAt { get; init; }
    public string? SourceUrl { get; init; }
}

public sealed record ProductDetail : ProductSummary
{
    public IReadOnlyList<ChargeVersionDto> ChargeVersions { get; init; } = [];
}

public sealed record AssetAllocationDto(decimal EquityPct, decimal FixedInterestPct, decimal PropertyPct, decimal CashPct, decimal AlternativesPct);

public sealed record FundStatisticsDto(decimal? Return1YPct, decimal? Return3YPct, decimal? Return5YPct, decimal? Volatility3YPct, decimal? Sharpe3Y, decimal? MaxDrawdown3YPct, decimal? YieldPct, int? MorningstarRating, string? MedalistRating);

public sealed record FundDto(
    Guid? Id,
    string Isin,
    string? Sedol,
    string Name,
    string? ShareClass,
    string ManagerName,
    FundType Type,
    string? IaSector,
    string? MorningstarCategory,
    decimal OcfPct,
    decimal TransactionCostsPct,
    AssetAllocationDto AssetAllocation,
    int? Srri,
    FundStatisticsDto Statistics,
    decimal? Price,
    DateOnly? PriceDate,
    string? FactsheetUrl,
    string? SourceUrl,
    DateOnly? AsAt);

public sealed record ModelPortfolioHoldingDto(Guid FundId, string Isin, string Name, decimal WeightPct, decimal OcfPct);

public sealed record ModelPortfolioDto(Guid Id, Guid ProviderId, string ProviderName, string Name, int RiskLevel, decimal MpsFeePct, decimal BlendedOcfPct, decimal TotalInvestmentChargePct, IReadOnlyList<ModelPortfolioHoldingDto> Holdings);

// --- Assumptions ---

public sealed record MarketInputsDto(decimal GiltYieldUpTo5Pct, decimal GiltYield5To10Pct, decimal GiltYield10To15Pct, decimal GiltYieldOver15Pct, decimal TvcAnnuityRateRpiLinkedPct, decimal TvcAnnuityRateLevelPct, decimal Cobs13YPct, DateOnly AsAt);

public record AssumptionSetWrite
{
    public required string Name { get; init; }
    public decimal GrowthLowerPct { get; init; }
    public decimal GrowthIntermediatePct { get; init; }
    public decimal GrowthHigherPct { get; init; }
    public decimal InflationPct { get; init; }
    public decimal EarningsGrowthPct { get; init; }
    public decimal RpiInflationPct { get; init; }
    public decimal ChargeInflationPct { get; init; }
    public decimal PreRetirementProductChargePct { get; init; }
    public decimal AnnuityExpenseLoadingPct { get; init; }
    public int SpouseAgeGapYears { get; init; }
    public Domain.Assumptions.MortalityBasis MortalityBasis { get; init; }
    public decimal StatePensionIncreasePct { get; init; }
    public required string TaxYear { get; init; }
    public Domain.Assumptions.ProjectionBasis ProjectionBasis { get; init; }
    public required MarketInputsDto MarketInputs { get; init; }
}

public sealed record AssumptionSetDto : AssumptionSetWrite
{
    public required Guid Id { get; init; }
    public Guid? FirmId { get; init; }
    public bool IsFcaStandard { get; init; }
    public int Version { get; init; }
}

public sealed record CopyAssumptionSetRequest(string Name);

/// <summary>Adviser overrides applied on top of an assumption set for one calculation (captured for audit).</summary>
public sealed record AssumptionOverrides(decimal? GrowthLowerPct, decimal? GrowthIntermediatePct, decimal? GrowthHigherPct, decimal? InflationPct, decimal? EarningsGrowthPct, decimal? StatePensionIncreasePct);

// --- Reports, audit, integrations, analyses summaries ---

public sealed record ReportDto(Guid Id, Guid ClientId, Guid AnalysisId, int AnalysisVersion, string AnalysisResultHash, ReportKind Kind, ReportFormat Format, string TemplateVersion, Guid GeneratedBy, string Sha256, long SizeBytes, DateTime GeneratedAtUtc, string DownloadUrl);

public sealed record CreateReportRequest(Guid AnalysisId, ReportKind Kind, ReportFormat Format);

public sealed record ReportFile(ReadOnlyMemory<byte> Content, string ContentType, string FileName);

public sealed record AuditEventDto(Guid Id, long Sequence, Guid? UserId, DateTime OccurredAtUtc, string EntityType, Guid? EntityId, string Action, string Payload, string PreviousHash, string Hash);

public sealed record ChainVerificationDto(bool IsValid, int FirstBrokenIndex, string? Reason, int EventsChecked);

public sealed record IntegrationStatusDto(string Connector, bool Configured, string Mode, DateTime? LastSyncUtc);

public sealed record ImportRequest(string? ExternalClientId);

public sealed record ImportResult(int Imported, int Updated, int Skipped, IReadOnlyList<string> Messages);

public sealed record SyncResult(int FundsUpdated, IReadOnlyList<string> Messages);

public enum AnalysisKind
{
    PensionSwitch = 0,
    DbTransfer = 1,
    Cashflow = 2,
}

public sealed record AnalysisSummary(Guid Id, AnalysisKind Kind, string Title, AnalysisStatus Status, int Version, DateTime? CalculatedAtUtc, DateTime UpdatedAtUtc, Guid CreatedBy, Guid ClientId);
