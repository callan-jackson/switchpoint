using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Schemes;

public enum ContributionPayer
{
    Member = 0,
    Employer = 1,
    ThirdParty = 2,
}

/// <summary>A regular or single contribution into an arrangement.</summary>
public sealed record Contribution
{
    public Contribution(
        ContributionPayer payer,
        decimal amount,
        Frequency frequency,
        decimal escalationRate = 0m,
        bool isGrossOfTaxRelief = true,
        int? startMonth = null,
        int? endMonth = null)
    {
        Payer = Guard.Defined(payer);
        Amount = Guard.NonNegative(amount);
        Frequency = Guard.Defined(frequency);
        EscalationRate = Guard.InRange(escalationRate, -0.5m, 0.5m);
        IsGrossOfTaxRelief = isGrossOfTaxRelief;
        if (startMonth is { } s)
        {
            Guard.Positive(s, nameof(startMonth));
        }

        if (endMonth is { } e)
        {
            Guard.Positive(e, nameof(endMonth));
            Guard.Against(startMonth is { } s2 && e < s2, "A contribution cannot end before it starts.");
        }

        StartMonth = startMonth;
        EndMonth = endMonth;
    }

    public ContributionPayer Payer { get; }

    /// <summary>Amount per payment (per month for monthly, per year for annual, the whole amount for single).</summary>
    public decimal Amount { get; }

    public Frequency Frequency { get; }

    /// <summary>Annual escalation applied at each plan anniversary.</summary>
    public decimal EscalationRate { get; }

    /// <summary>
    /// When false the amount is what the member pays net; relief at source grosses it up by the
    /// basic rate (20% ⇒ ÷0.8). Employer contributions are always gross.
    /// </summary>
    public bool IsGrossOfTaxRelief { get; }

    /// <summary>First projection month (1-based) in which the contribution is paid; null = month 1.</summary>
    public int? StartMonth { get; }

    /// <summary>Last projection month in which the contribution is paid; null = to the end of the projection.</summary>
    public int? EndMonth { get; }

    public decimal AnnualisedAmount => Frequency.Annualise(Amount);
}
