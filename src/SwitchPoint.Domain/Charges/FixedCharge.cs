using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>A monetary charge in pounds levied at a fixed frequency, optionally indexed each year.</summary>
public sealed record FixedCharge
{
    public FixedCharge(decimal amount, Frequency frequency, Indexation? indexation = null, FixedChargeScope appliesTo = FixedChargeScope.Wrapper, string? description = null)
    {
        Amount = Guard.NonNegative(amount);
        Frequency = Guard.Defined(frequency);
        Indexation = indexation ?? Indexation.None;
        AppliesTo = Guard.Defined(appliesTo);
        Description = description;
    }

    /// <summary>Amount in pounds per payment.</summary>
    public decimal Amount { get; }

    public Frequency Frequency { get; }

    public Indexation Indexation { get; }

    public FixedChargeScope AppliesTo { get; }

    public string? Description { get; }

    /// <summary>Total in the first year: amount times payments per year, or the amount itself for a one-off.</summary>
    public decimal AnnualAmount => Frequency.Annualise(Amount);

    /// <summary>
    /// Total charged in plan year <paramref name="year"/> (1-based). A one-off is charged only in year 1;
    /// recurring charges are indexed from year 2 using <paramref name="cpiAssumption"/> where the basis is CPI.
    /// </summary>
    public decimal AmountInYear(int year, decimal cpiAssumption = 0m)
    {
        Guard.Positive(year);
        if (Frequency == Frequency.Single)
        {
            return year == 1 ? Amount : 0m;
        }

        return AnnualAmount * Indexation.FactorForYear(year, cpiAssumption);
    }
}
