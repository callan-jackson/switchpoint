using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>
/// An annual percentage charge tiered by fund value. Bands are ordered ascending by their inclusive
/// upper bound, are contiguous by construction (each band starts where the previous ends, the first at
/// zero) and the last band is unbounded. A value exactly on a band edge belongs to the lower band.
/// </summary>
public sealed record TieredCharge
{
    public TieredCharge(IEnumerable<TierBand> bands, TieredChargeMode mode)
    {
        var list = Guard.NotEmpty(bands);
        Mode = Guard.Defined(mode);

        for (var i = 0; i < list.Count; i++)
        {
            var band = list[i];
            var isLast = i == list.Count - 1;
            if (isLast)
            {
                Guard.Against(band.UpTo is not null, "The last band of a tiered charge must be unbounded (UpTo = null).");
            }
            else
            {
                Guard.Against(band.UpTo is null, $"Band {i} is unbounded but is not the last band; only the last band may have UpTo = null.");
                if (i > 0)
                {
                    Guard.Against(
                        band.UpTo <= list[i - 1].UpTo,
                        $"Band {i} upper bound {band.UpTo} must be greater than the previous band's upper bound {list[i - 1].UpTo}.");
                }
            }
        }

        Bands = list;
    }

    /// <summary>Bands in ascending order; the final band has <see cref="TierBand.UpTo"/> = null.</summary>
    public IReadOnlyList<TierBand> Bands { get; }

    public TieredChargeMode Mode { get; }

    /// <summary>Rate of the final, unbounded band.</summary>
    public decimal TopBandRate => Bands[^1].AnnualRate;

    public decimal MaxRate => Bands.Max(b => b.AnnualRate);

    public decimal MinRate => Bands.Min(b => b.AnnualRate);

    /// <summary>A single-band charge at a flat rate.</summary>
    public static TieredCharge Flat(decimal annualRate, TieredChargeMode mode = TieredChargeMode.Marginal)
        => new([new TierBand(null, annualRate)], mode);

    /// <summary>Builds a marginal tiered charge from (upTo, rate) tuples; the last tuple must have a null bound.</summary>
    public static TieredCharge Marginal(params (decimal? UpTo, decimal AnnualRate)[] bands)
        => new(bands.Select(b => new TierBand(b.UpTo, b.AnnualRate)), TieredChargeMode.Marginal);

    /// <summary>Builds a whole-of-fund tiered charge from (upTo, rate) tuples; the last tuple must have a null bound.</summary>
    public static TieredCharge WholeOfFund(params (decimal? UpTo, decimal AnnualRate)[] bands)
        => new(bands.Select(b => new TierBand(b.UpTo, b.AnnualRate)), TieredChargeMode.WholeOfFund);

    /// <summary>Lower (exclusive, except zero) bound of a band: zero for the first band, otherwise the previous band's upper bound.</summary>
    public decimal LowerBoundOf(int bandIndex)
    {
        Guard.InRange(bandIndex, 0, Bands.Count - 1);
        return bandIndex == 0 ? 0m : Bands[bandIndex - 1].UpTo!.Value;
    }

    /// <summary>Index of the band that contains <paramref name="fundValue"/>; a value exactly on an edge belongs to the lower band.</summary>
    public int BandIndexFor(decimal fundValue)
    {
        Guard.NonNegative(fundValue);
        for (var i = 0; i < Bands.Count; i++)
        {
            var upTo = Bands[i].UpTo;
            if (upTo is null || fundValue <= upTo.Value)
            {
                return i;
            }
        }

        return Bands.Count - 1;
    }

    public TierBand BandFor(decimal fundValue) => Bands[BandIndexFor(fundValue)];

    /// <summary>
    /// The annual charge in pounds for a fund of the given value. Marginal mode charges each slice at
    /// its band rate; whole-of-fund mode charges the entire value at the containing band's rate.
    /// </summary>
    public decimal AnnualChargeFor(decimal fundValue)
    {
        Guard.NonNegative(fundValue);
        if (fundValue == 0m)
        {
            return 0m;
        }

        return Mode == TieredChargeMode.WholeOfFund
            ? BandFor(fundValue).AnnualRate * fundValue
            : MarginalChargeFor(fundValue);
    }

    /// <summary>Annual charge divided by fund value. For a zero value the first band's rate is returned (the limit as the value tends to zero).</summary>
    public decimal EffectiveRateFor(decimal fundValue)
    {
        Guard.NonNegative(fundValue);
        return fundValue == 0m ? Bands[0].AnnualRate : AnnualChargeFor(fundValue) / fundValue;
    }

    public bool Equals(TieredCharge? other)
        => other is not null && Mode == other.Mode && Bands.SequenceEqual(other.Bands);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mode);
        foreach (var band in Bands)
        {
            hash.Add(band);
        }

        return hash.ToHashCode();
    }

    private decimal MarginalChargeFor(decimal fundValue)
    {
        var total = 0m;
        var lower = 0m;
        foreach (var band in Bands)
        {
            var sliceTop = band.UpTo is { } upper ? Math.Min(fundValue, upper) : fundValue;
            if (sliceTop <= lower)
            {
                break;
            }

            total += (sliceTop - lower) * band.AnnualRate;
            if (band.UpTo is null || fundValue <= band.UpTo.Value)
            {
                break;
            }

            lower = band.UpTo.Value;
        }

        return total;
    }
}
