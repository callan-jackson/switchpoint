using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Domain.Analysis;

/// <summary>An APTA/TVC analysis of a defined benefit transfer (COBS 19.1).</summary>
public sealed class DbTransferAnalysis : AnalysisBase
{
    private readonly List<Holding> _proposedHoldings = [];

    public DbTransferAnalysis(Guid id, Guid firmId, Guid clientId, Guid dbSchemeId, Guid assumptionSetId, Guid createdBy, DateOnly transferDate, DateTime createdAtUtc)
        : base(id, firmId, clientId, assumptionSetId, createdBy, createdAtUtc)
    {
        DbSchemeId = Guard.NotEmpty(dbSchemeId);
        TransferDate = transferDate;
    }

    public Guid DbSchemeId { get; }
    public DateOnly TransferDate { get; private set; }
    public Guid? ProposedProductId { get; private set; }
    public int? ProposedProductChargeVersion { get; private set; }
    public IReadOnlyList<Holding> ProposedHoldings => _proposedHoldings;
    public AdviserCharge ProposedAdviserCharges { get; private set; } = AdviserCharge.None;
    public AdviserChargeBasis ChargeBasis { get; private set; } = AdviserChargeBasis.NonContingent;

    /// <summary>The COBS 19.1B.9R carve-out relied on when charging contingently, if any.</summary>
    public string? ContingentChargingCarveOut { get; private set; }

    /// <summary>Whether an available qualifying (workplace) scheme was compared, per COBS 19.1.2BR(3).</summary>
    public Guid? WorkplaceSchemeProductId { get; private set; }

    /// <summary>Adviser-chosen growth rate reflecting the proposed investments (COBS 19 Annex 4A 1R(1)).</summary>
    public decimal? AptaGrowthRate { get; private set; }

    public int PlanEndAge { get; private set; } = 100;

    public void SetProposal(Guid productId, int productChargeVersion, IEnumerable<Holding> holdings, AdviserCharge adviserCharges, decimal aptaGrowthRate, DateTime nowUtc)
    {
        ProposedProductId = Guard.NotEmpty(productId);
        ProposedProductChargeVersion = Guard.Positive(productChargeVersion);
        List<Holding> list = [.. Guard.NotNull(holdings)];
        decimal total = list.Sum(h => h.Weight);
        Guard.Against(list.Count == 0 || Math.Abs(total - 1m) > 0.000001m, "Proposed holdings are required and must sum to 1.");
        _proposedHoldings.Clear();
        _proposedHoldings.AddRange(list);
        ProposedAdviserCharges = Guard.NotNull(adviserCharges);
        AptaGrowthRate = Guard.InRange(aptaGrowthRate, -0.1m, 0.2m);
        Invalidate(nowUtc);
    }

    public void SetChargeBasis(AdviserChargeBasis basis, string? carveOut, DateTime nowUtc)
    {
        EnsureEditable();
        ChargeBasis = Guard.Defined(basis);
        Guard.Against(basis == AdviserChargeBasis.Contingent && string.IsNullOrWhiteSpace(carveOut), "Contingent charging requires a recorded COBS 19.1B.9R carve-out (serious ill-health or serious financial difficulty).");
        ContingentChargingCarveOut = basis == AdviserChargeBasis.Contingent ? carveOut : null;
        Touch(nowUtc);
    }

    public void SetWorkplaceComparison(Guid? workplaceProductId, DateTime nowUtc)
    {
        WorkplaceSchemeProductId = workplaceProductId;
        Invalidate(nowUtc);
    }

    public void SetTransferDate(DateOnly transferDate, int planEndAge, DateTime nowUtc)
    {
        TransferDate = transferDate;
        PlanEndAge = Guard.InRange(planEndAge, 75, 110);
        Invalidate(nowUtc);
    }

    public bool IsReadyToCalculate => ProposedProductId is not null && AptaGrowthRate is not null;
}
