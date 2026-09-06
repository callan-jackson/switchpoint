using SwitchPoint.Domain.Analysis;

namespace SwitchPoint.Application.Dtos;

public record PensionSwitchAnalysisWrite
{
    public required Guid ClientId { get; init; }
    public required string Title { get; init; }
    public required int RetirementAge { get; init; }
    public IReadOnlyList<Guid> CedingSchemeIds { get; init; } = [];
    public Guid? ProposedProductId { get; init; }
    public int? ProposedProductChargeVersion { get; init; }
    public IReadOnlyList<HoldingDto> ProposedHoldings { get; init; } = [];
    public Guid? ProposedModelPortfolioId { get; init; }
    public AdviserChargeDto ProposedAdviserCharges { get; init; } = AdviserChargeDto.None;
    public required Guid AssumptionSetId { get; init; }
    public AssumptionOverrides? Overrides { get; init; }
    public string? Rationale { get; init; }
}

public sealed record PensionSwitchAnalysisDto : PensionSwitchAnalysisWrite
{
    public required Guid Id { get; init; }
    public required Guid FirmId { get; init; }
    public AnalysisStatus Status { get; init; }
    public int Version { get; init; }
    public string? ResultHash { get; init; }
    public DateTime? CalculatedAtUtc { get; init; }
    public string? EngineVersion { get; init; }
    public PensionSwitchResultDto? Result { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public Guid CreatedBy { get; init; }
}

public record DbTransferAnalysisWrite
{
    public required Guid ClientId { get; init; }
    public required Guid DbSchemeId { get; init; }
    public required DateOnly TransferDate { get; init; }
    public int PlanEndAge { get; init; } = 100;
    public Guid? ProposedProductId { get; init; }
    public int? ProposedProductChargeVersion { get; init; }
    public IReadOnlyList<HoldingDto> ProposedHoldings { get; init; } = [];
    public AdviserChargeDto ProposedAdviserCharges { get; init; } = AdviserChargeDto.None;
    public decimal? AptaGrowthPct { get; init; }
    public AdviserChargeBasis ChargeBasis { get; init; } = AdviserChargeBasis.NonContingent;
    public string? ContingentChargingCarveOut { get; init; }
    public Guid? WorkplaceSchemeProductId { get; init; }
    public decimal InitialAdviceFee { get; init; }
    public decimal? WorkplaceDefaultChargePct { get; init; }
    public required Guid AssumptionSetId { get; init; }
    public AssumptionOverrides? Overrides { get; init; }
}

public sealed record DbTransferAnalysisDto : DbTransferAnalysisWrite
{
    public required Guid Id { get; init; }
    public required Guid FirmId { get; init; }
    public AnalysisStatus Status { get; init; }
    public int Version { get; init; }
    public string? ResultHash { get; init; }
    public DateTime? CalculatedAtUtc { get; init; }
    public string? EngineVersion { get; init; }
    public DbTransferResultDto? Result { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public Guid CreatedBy { get; init; }
}

public record CashflowPlanWrite
{
    public required Guid ClientId { get; init; }
    public Guid? PartnerClientId { get; init; }
    public required string Title { get; init; }
    public int PlanEndAge { get; init; } = 100;
    public IReadOnlyList<PlanIncomeDto> Incomes { get; init; } = [];
    public IReadOnlyList<PlanExpenseDto> Expenses { get; init; } = [];
    public IReadOnlyList<PlanAssetDto> Assets { get; init; } = [];
    public IReadOnlyList<PlanEventDto> Events { get; init; } = [];
    public PlanStrategyDto Strategy { get; init; } = PlanStrategyDto.Default;
    public ulong? StochasticSeed { get; init; }
    public int StochasticPaths { get; init; } = 1000;
    public required Guid AssumptionSetId { get; init; }
    public AssumptionOverrides? Overrides { get; init; }
}

public sealed record CashflowPlanDto : CashflowPlanWrite
{
    public required Guid Id { get; init; }
    public required Guid FirmId { get; init; }
    public AnalysisStatus Status { get; init; }
    public int Version { get; init; }
    public string? ResultHash { get; init; }
    public DateTime? CalculatedAtUtc { get; init; }
    public string? EngineVersion { get; init; }
    public CashflowResultDto? Result { get; init; }
    public StochasticResultDto? StochasticResult { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public Guid CreatedBy { get; init; }
}
