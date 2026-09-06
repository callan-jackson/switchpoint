using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>A charge in pounds per fund switch, with the expected number of switches a year.</summary>
public sealed record SwitchCharge
{
    public SwitchCharge(decimal amountPerSwitch = 0m, int expectedSwitchesPerYear = 0)
    {
        AmountPerSwitch = Guard.NonNegative(amountPerSwitch);
        ExpectedSwitchesPerYear = Guard.NonNegative(expectedSwitchesPerYear);
    }

    public decimal AmountPerSwitch { get; }

    public int ExpectedSwitchesPerYear { get; }

    public static SwitchCharge None { get; } = new();

    /// <summary>Expected switching cost in pounds per year.</summary>
    public decimal AnnualCost => AmountPerSwitch * ExpectedSwitchesPerYear;
}
