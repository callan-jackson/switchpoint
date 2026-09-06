using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Market;

/// <summary>A pension or investment product (wrapper) offered by a provider, with versioned charges.</summary>
public sealed class Product : Entity
{
    private readonly List<ProductChargeVersion> _chargeVersions = [];

    /// <summary>For EF Core materialisation only.</summary>
    private Product()
    {
        Name = null!;
    }

    public Product(
        Guid id,
        Guid providerId,
        string name,
        WrapperTypes wrapperTypes,
        DateTime createdAtUtc,
        decimal minimumInvestment = 0m,
        decimal minimumRegularContribution = 0m,
        bool allowsFamilyLinking = false,
        FundUniverse fundUniverse = FundUniverse.WholeOfMarket)
        : base(id, createdAtUtc)
    {
        ProviderId = Guard.NotEmpty(providerId);
        Name = Guard.NotNullOrWhiteSpace(name);
        Guard.Against(wrapperTypes == WrapperTypes.None, "A product must support at least one wrapper type.");
        WrapperTypes = wrapperTypes;
        MinimumInvestment = Guard.NonNegative(minimumInvestment);
        MinimumRegularContribution = Guard.NonNegative(minimumRegularContribution);
        AllowsFamilyLinking = allowsFamilyLinking;
        FundUniverse = Guard.Defined(fundUniverse);
    }

    public Guid ProviderId { get; }
    public string Name { get; private set; }
    public WrapperTypes WrapperTypes { get; private set; }
    public decimal MinimumInvestment { get; private set; }
    public decimal MinimumRegularContribution { get; private set; }
    public bool AllowsFamilyLinking { get; private set; }
    public FundUniverse FundUniverse { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyList<ProductChargeVersion> ChargeVersions => _chargeVersions;

    public bool Supports(WrapperTypes wrapper) => (WrapperTypes & wrapper) == wrapper;

    /// <summary>Adds a new charge version, closing the previous open version the day before it starts.</summary>
    public ProductChargeVersion AddChargeVersion(ChargeSchedule charges, DateOnly effectiveFrom, string? sourceUrl, DateOnly asAt, DataQuality quality, DateTime nowUtc)
    {
        Guard.NotNull(charges);
        ProductChargeVersion? latest = _chargeVersions.LastOrDefault();
        if (latest is not null)
        {
            Guard.Against(effectiveFrom <= latest.EffectiveFrom, "A new charge version must start after the previous one.");
            _chargeVersions[^1] = latest.ClosedOn(effectiveFrom.AddDays(-1));
        }

        ProductChargeVersion version = new(_chargeVersions.Count + 1, charges, effectiveFrom, null, sourceUrl, asAt, quality);
        _chargeVersions.Add(version);
        Touch(nowUtc);
        return version;
    }

    public ProductChargeVersion? ChargesOn(DateOnly date) => _chargeVersions.LastOrDefault(v => v.IsEffectiveOn(date));

    public ProductChargeVersion? CurrentCharges => _chargeVersions.LastOrDefault();

    public ProductChargeVersion Version(int version) =>
        _chargeVersions.SingleOrDefault(v => v.Version == version) ?? throw new DomainException($"Product '{Name}' has no charge version {version}.");

    public void Update(string name, WrapperTypes wrapperTypes, decimal minimumInvestment, decimal minimumRegularContribution, bool allowsFamilyLinking, FundUniverse fundUniverse, DateTime nowUtc)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Guard.Against(wrapperTypes == WrapperTypes.None, "A product must support at least one wrapper type.");
        WrapperTypes = wrapperTypes;
        MinimumInvestment = Guard.NonNegative(minimumInvestment);
        MinimumRegularContribution = Guard.NonNegative(minimumRegularContribution);
        AllowsFamilyLinking = allowsFamilyLinking;
        FundUniverse = Guard.Defined(fundUniverse);
        Touch(nowUtc);
    }

    public void Withdraw(DateTime nowUtc)
    {
        IsActive = false;
        Touch(nowUtc);
    }
}
