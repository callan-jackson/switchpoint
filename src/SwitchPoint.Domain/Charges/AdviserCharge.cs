using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>Adviser remuneration: an initial charge (rate of the amount invested and/or a fixed sum) and an ongoing annual charge.</summary>
public sealed record AdviserCharge
{
    public AdviserCharge(decimal initialRate = 0m, decimal initialAmount = 0m, decimal ongoingRate = 0m, decimal ongoingAmount = 0m)
    {
        InitialRate = Guard.Fraction(initialRate);
        InitialAmount = Guard.NonNegative(initialAmount);
        OngoingRate = Guard.Fraction(ongoingRate);
        OngoingAmount = Guard.NonNegative(ongoingAmount);
    }

    /// <summary>Initial charge as a fraction of the amount invested.</summary>
    public decimal InitialRate { get; }

    /// <summary>Initial charge as a fixed sum in pounds.</summary>
    public decimal InitialAmount { get; }

    /// <summary>Ongoing charge as a fraction of fund value per year.</summary>
    public decimal OngoingRate { get; }

    /// <summary>Ongoing charge as a fixed sum in pounds per year.</summary>
    public decimal OngoingAmount { get; }

    public static AdviserCharge None { get; } = new();

    public bool IsNone => InitialRate == 0m && InitialAmount == 0m && OngoingRate == 0m && OngoingAmount == 0m;

    public decimal InitialFor(decimal amountInvested) => Guard.NonNegative(amountInvested) * InitialRate + InitialAmount;

    public decimal OngoingAnnualFor(decimal fundValue) => Guard.NonNegative(fundValue) * OngoingRate + OngoingAmount;
}
