using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>
/// One band of a <see cref="TieredCharge"/>: an inclusive upper bound in pounds (null for the final,
/// unbounded band) and the annual rate (a fraction) charged in that band.
/// </summary>
public sealed record TierBand
{
    public TierBand(decimal? upTo, decimal annualRate)
    {
        if (upTo is { } bound)
        {
            Guard.Positive(bound, nameof(upTo));
        }

        UpTo = upTo;
        AnnualRate = Guard.Fraction(annualRate);
    }

    /// <summary>Inclusive upper bound of the band in pounds; null means the band is unbounded.</summary>
    public decimal? UpTo { get; }

    /// <summary>Annual charge rate as a fraction (0.0035m is 0.35% a year).</summary>
    public decimal AnnualRate { get; }

    public bool IsUnbounded => UpTo is null;
}
