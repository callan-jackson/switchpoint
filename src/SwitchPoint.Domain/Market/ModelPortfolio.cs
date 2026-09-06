using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Market;

public sealed record ModelPortfolioHolding(Guid FundId, string Isin, string Name, decimal Weight, decimal Ocf);

/// <summary>A discretionary model portfolio service (MPS) with its own fee layered over the underlying funds.</summary>
public sealed class ModelPortfolio : Entity
{
    private readonly List<ModelPortfolioHolding> _holdings = [];

    /// <summary>For EF Core materialisation only.</summary>
    private ModelPortfolio()
    {
        Name = null!;
    }

    public ModelPortfolio(Guid id, Guid providerId, string name, int riskLevel, decimal mpsFee, DateTime createdAtUtc)
        : base(id, createdAtUtc)
    {
        ProviderId = Guard.NotEmpty(providerId);
        Name = Guard.NotNullOrWhiteSpace(name);
        RiskLevel = Guard.InRange(riskLevel, 1, 10);
        MpsFee = Guard.InRange(mpsFee, 0m, 0.05m);
    }

    public Guid ProviderId { get; }
    public string Name { get; private set; }
    public int RiskLevel { get; private set; }

    /// <summary>Annual DFM/MPS fee as a fraction, charged in addition to fund OCFs.</summary>
    public decimal MpsFee { get; private set; }

    public IReadOnlyList<ModelPortfolioHolding> Holdings => _holdings;

    /// <summary>Weighted OCF of the underlying funds (excluding the MPS fee).</summary>
    public decimal BlendedOcf => _holdings.Count == 0 ? 0m : _holdings.Sum(h => h.Weight * h.Ocf);

    /// <summary>Total investment cost the client bears: MPS fee plus blended OCF.</summary>
    public decimal TotalInvestmentCharge => MpsFee + BlendedOcf;

    public void ReplaceHoldings(IEnumerable<ModelPortfolioHolding> holdings, DateTime nowUtc)
    {
        List<ModelPortfolioHolding> list = [.. Guard.NotNull(holdings)];
        decimal total = list.Sum(h => h.Weight);
        Guard.Against(list.Count > 0 && Math.Abs(total - 1m) > 0.000001m, $"Holding weights must sum to 1 (got {total}).");
        _holdings.Clear();
        _holdings.AddRange(list);
        Touch(nowUtc);
    }

    public void Update(string name, int riskLevel, decimal mpsFee, DateTime nowUtc)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        RiskLevel = Guard.InRange(riskLevel, 1, 10);
        MpsFee = Guard.InRange(mpsFee, 0m, 0.05m);
        Touch(nowUtc);
    }
}
