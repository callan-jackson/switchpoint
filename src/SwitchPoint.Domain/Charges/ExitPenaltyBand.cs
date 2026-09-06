using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>
/// One band of an <see cref="ExitPenaltySchedule"/>: while fewer than <see cref="UntilYearsFromStart"/> years
/// have elapsed the penalty is <see cref="Rate"/> of the value plus <see cref="Amount"/> in pounds.
/// A null bound means the band never expires.
/// </summary>
public sealed record ExitPenaltyBand
{
    public ExitPenaltyBand(decimal? untilYearsFromStart, decimal rate, decimal amount = 0m)
    {
        if (untilYearsFromStart is { } until)
        {
            Guard.Positive(until, nameof(untilYearsFromStart));
        }

        UntilYearsFromStart = untilYearsFromStart;
        Rate = Guard.Fraction(rate);
        Amount = Guard.NonNegative(amount);
    }

    /// <summary>Exclusive upper bound in years since the plan started; null means open-ended.</summary>
    public decimal? UntilYearsFromStart { get; }

    /// <summary>Penalty as a fraction of the value transferred.</summary>
    public decimal Rate { get; }

    /// <summary>Penalty as a fixed sum in pounds.</summary>
    public decimal Amount { get; }

    public bool IsOpenEnded => UntilYearsFromStart is null;

    public bool AppliesAt(decimal yearsSinceStart)
        => UntilYearsFromStart is null || Guard.NonNegative(yearsSinceStart) < UntilYearsFromStart.Value;
}
