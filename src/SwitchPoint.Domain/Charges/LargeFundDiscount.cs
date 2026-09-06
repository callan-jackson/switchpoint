using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>A rebate (negative annual charge) of <see cref="RebateRate"/> on the whole fund once its value reaches <see cref="Threshold"/>.</summary>
public sealed record LargeFundDiscount
{
    public LargeFundDiscount(decimal threshold, decimal rebateRate)
    {
        Threshold = Guard.Positive(threshold);
        RebateRate = Guard.Fraction(rebateRate);
    }

    /// <summary>Fund value in pounds at or above which the rebate applies.</summary>
    public decimal Threshold { get; }

    /// <summary>Annual rebate as a fraction of the whole fund value.</summary>
    public decimal RebateRate { get; }

    public bool AppliesTo(decimal fundValue) => Guard.NonNegative(fundValue) >= Threshold;
}
