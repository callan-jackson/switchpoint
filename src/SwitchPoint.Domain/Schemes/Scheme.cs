using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Schemes;

/// <summary>An existing arrangement (pension or investment) held by a client.</summary>
public class Scheme : Entity, ITenantScoped
{
    private readonly List<Contribution> _contributions = [];
    private readonly List<Holding> _holdings = [];

    /// <summary>For EF Core materialisation only.</summary>
    protected Scheme()
    {
        ProductName = null!;
    }

    public Scheme(
        Guid id,
        Guid firmId,
        Guid clientId,
        SchemeType type,
        string productName,
        decimal currentValue,
        decimal transferValue,
        DateOnly valuationDate,
        DateTime createdAtUtc,
        Guid? providerId = null,
        string? policyNumber = null)
        : base(id, createdAtUtc)
    {
        FirmId = Guard.NotEmpty(firmId);
        ClientId = Guard.NotEmpty(clientId);
        Type = Guard.Defined(type);
        ProductName = Guard.NotNullOrWhiteSpace(productName);
        CurrentValue = Guard.NonNegative(currentValue);
        TransferValue = Guard.NonNegative(transferValue);
        ValuationDate = valuationDate;
        ProviderId = providerId;
        PolicyNumber = policyNumber;
    }

    public Guid FirmId { get; }
    public Guid ClientId { get; }
    public SchemeType Type { get; }
    public Guid? ProviderId { get; private set; }
    public string ProductName { get; private set; }
    public string? PolicyNumber { get; private set; }
    public decimal CurrentValue { get; private set; }

    /// <summary>The value available on transfer (after any MVR or penalty the provider has already quoted).</summary>
    public decimal TransferValue { get; private set; }

    public DateOnly ValuationDate { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public ChargeSchedule Charges { get; private set; } = ChargeSchedule.None;
    public Guarantees Guarantees { get; private set; } = Guarantees.None;
    public int? SelectedRetirementAge { get; private set; }
    public bool InDrawdown { get; private set; }
    public IReadOnlyList<Contribution> Contributions => _contributions;
    public IReadOnlyList<Holding> Holdings => _holdings;

    public void UpdateValuation(decimal currentValue, decimal transferValue, DateOnly valuationDate, DateTime nowUtc)
    {
        CurrentValue = Guard.NonNegative(currentValue);
        TransferValue = Guard.NonNegative(transferValue);
        ValuationDate = valuationDate;
        Touch(nowUtc);
    }

    public void SetProduct(Guid? providerId, string productName, string? policyNumber, DateOnly? startDate, DateTime nowUtc)
    {
        ProviderId = providerId;
        ProductName = Guard.NotNullOrWhiteSpace(productName);
        PolicyNumber = policyNumber;
        StartDate = startDate;
        Touch(nowUtc);
    }

    public void SetCharges(ChargeSchedule charges, DateTime nowUtc)
    {
        Charges = Guard.NotNull(charges);
        Touch(nowUtc);
    }

    public void SetGuarantees(Guarantees guarantees, DateTime nowUtc)
    {
        Guarantees = Guard.NotNull(guarantees);
        Touch(nowUtc);
    }

    public void SetRetirementAge(int? age, DateTime nowUtc)
    {
        if (age is { } a)
        {
            Guard.InRange(a, 50, 80, nameof(age));
        }

        SelectedRetirementAge = age;
        Touch(nowUtc);
    }

    public void SetDrawdown(bool inDrawdown, DateTime nowUtc)
    {
        InDrawdown = inDrawdown;
        Touch(nowUtc);
    }

    public void ReplaceContributions(IEnumerable<Contribution> contributions, DateTime nowUtc)
    {
        _contributions.Clear();
        _contributions.AddRange(Guard.NotNull(contributions));
        Touch(nowUtc);
    }

    public void ReplaceHoldings(IEnumerable<Holding> holdings, DateTime nowUtc)
    {
        List<Holding> list = [.. Guard.NotNull(holdings)];
        decimal total = list.Sum(h => h.Weight);
        Guard.Against(list.Count > 0 && Math.Abs(total - 1m) > 0.000001m, $"Holding weights must sum to 1 (got {total}).");
        _holdings.Clear();
        _holdings.AddRange(list);
        Touch(nowUtc);
    }

    /// <summary>Years the plan has been in force at <paramref name="asAt"/>, for exit penalty schedules; 0 when the start date is unknown.</summary>
    public decimal YearsInForce(DateOnly asAt)
    {
        if (StartDate is not { } start || asAt <= start)
        {
            return 0m;
        }

        return (asAt.DayNumber - start.DayNumber) / 365.25m;
    }

    /// <summary>Transfer value after applying the scheme's own exit penalty schedule at <paramref name="asAt"/>.</summary>
    public decimal NetTransferValue(DateOnly asAt) => TransferValue - Charges.ExitPenalty.PenaltyFor(YearsInForce(asAt), TransferValue);

    public decimal AnnualContributions => _contributions.Where(c => c.Frequency != Frequency.Single).Sum(c => c.AnnualisedAmount);
}
