using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Domain.Analysis;

/// <summary>A DC-to-DC switching analysis: one or more ceding schemes compared with a proposed product.</summary>
public sealed class PensionSwitchAnalysis : AnalysisBase
{
    private readonly List<Guid> _cedingSchemeIds = [];
    private readonly List<Holding> _proposedHoldings = [];

    /// <summary>For EF Core materialisation only.</summary>
    private PensionSwitchAnalysis()
    {
        Title = null!;
    }

    public PensionSwitchAnalysis(Guid id, Guid firmId, Guid clientId, Guid assumptionSetId, Guid createdBy, string title, int retirementAge, DateTime createdAtUtc)
        : base(id, firmId, clientId, assumptionSetId, createdBy, createdAtUtc)
    {
        Title = Guard.NotNullOrWhiteSpace(title);
        RetirementAge = Guard.InRange(retirementAge, 50, 80);
    }

    public string Title { get; private set; }
    public int RetirementAge { get; private set; }
    public IReadOnlyList<Guid> CedingSchemeIds => _cedingSchemeIds;
    public Guid? ProposedProductId { get; private set; }
    public int? ProposedProductChargeVersion { get; private set; }
    public IReadOnlyList<Holding> ProposedHoldings => _proposedHoldings;
    public Guid? ProposedModelPortfolioId { get; private set; }
    public AdviserCharge ProposedAdviserCharges { get; private set; } = AdviserCharge.None;

    /// <summary>Non-cost reasons the adviser records for a switch (e.g. consolidation, fund choice, drawdown access).</summary>
    public string? Rationale { get; private set; }

    public void SetCedingSchemes(IEnumerable<Guid> schemeIds, DateTime nowUtc)
    {
        List<Guid> ids = [.. Guard.NotNull(schemeIds).Distinct()];
        Guard.Against(ids.Count == 0, "At least one ceding scheme is required.");
        Guard.Against(ids.Any(i => i == Guid.Empty), "Scheme identifiers must not be empty.");
        _cedingSchemeIds.Clear();
        _cedingSchemeIds.AddRange(ids);
        Invalidate(nowUtc);
    }

    public void SetProposal(Guid productId, int productChargeVersion, IEnumerable<Holding> holdings, Guid? modelPortfolioId, AdviserCharge adviserCharges, DateTime nowUtc)
    {
        ProposedProductId = Guard.NotEmpty(productId);
        ProposedProductChargeVersion = Guard.Positive(productChargeVersion);
        List<Holding> list = [.. Guard.NotNull(holdings)];
        decimal total = list.Sum(h => h.Weight);
        Guard.Against(list.Count > 0 && Math.Abs(total - 1m) > 0.000001m, $"Proposed holding weights must sum to 1 (got {total}).");
        Guard.Against(list.Count == 0 && modelPortfolioId is null, "A proposal needs holdings or a model portfolio.");
        _proposedHoldings.Clear();
        _proposedHoldings.AddRange(list);
        ProposedModelPortfolioId = modelPortfolioId;
        ProposedAdviserCharges = Guard.NotNull(adviserCharges);
        Invalidate(nowUtc);
    }

    public void SetRetirementAge(int retirementAge, DateTime nowUtc)
    {
        RetirementAge = Guard.InRange(retirementAge, 50, 80);
        Invalidate(nowUtc);
    }

    public void SetRationale(string? rationale, DateTime nowUtc)
    {
        EnsureEditable();
        Rationale = rationale;
        Touch(nowUtc);
    }

    public void Rename(string title, DateTime nowUtc)
    {
        EnsureEditable();
        Title = Guard.NotNullOrWhiteSpace(title);
        Touch(nowUtc);
    }

    public bool IsReadyToCalculate => _cedingSchemeIds.Count > 0 && ProposedProductId is not null;
}
