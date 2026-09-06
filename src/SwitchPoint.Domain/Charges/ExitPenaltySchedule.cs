using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>
/// Early-exit penalties as a list of bands ascending by expiry. The first band whose bound has not yet
/// been reached applies; a value exactly at a bound has moved to the next band. The penalty is capped
/// at the value transferred. An empty schedule means no penalty.
/// </summary>
public sealed record ExitPenaltySchedule
{
    public ExitPenaltySchedule(IEnumerable<ExitPenaltyBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        var list = bands.ToArray();
        for (var i = 0; i < list.Length; i++)
        {
            var isLast = i == list.Length - 1;
            Guard.Against(!isLast && list[i].IsOpenEnded, $"Exit penalty band {i} is open-ended but is not the last band.");
            if (i > 0 && !list[i].IsOpenEnded)
            {
                Guard.Against(
                    list[i].UntilYearsFromStart <= list[i - 1].UntilYearsFromStart,
                    $"Exit penalty band {i} must expire later than band {i - 1}.");
            }
        }

        Bands = list;
    }

    public IReadOnlyList<ExitPenaltyBand> Bands { get; }

    public static ExitPenaltySchedule None { get; } = new([]);

    public bool IsNone => Bands.Count == 0;

    /// <summary>Years after which no penalty applies, or null when the schedule is empty or open-ended-free.</summary>
    public decimal? ExpiresAfterYears => Bands.Count == 0 ? 0m : Bands[^1].UntilYearsFromStart;

    /// <summary>A single percentage penalty that expires after <paramref name="untilYears"/> years.</summary>
    public static ExitPenaltySchedule Percentage(decimal rate, decimal? untilYears)
        => new([new ExitPenaltyBand(untilYears, rate)]);

    /// <summary>A single fixed-sum penalty that expires after <paramref name="untilYears"/> years.</summary>
    public static ExitPenaltySchedule FixedAmount(decimal amount, decimal? untilYears)
        => new([new ExitPenaltyBand(untilYears, 0m, amount)]);

    /// <summary>A declining schedule from (untilYears, rate) tuples, e.g. (1, 5%), (2, 4%), (3, 3%).</summary>
    public static ExitPenaltySchedule Declining(params (decimal? UntilYears, decimal Rate)[] bands)
        => new(bands.Select(b => new ExitPenaltyBand(b.UntilYears, b.Rate)));

    /// <summary>The band in force after <paramref name="yearsSinceStart"/> years, or null if no penalty applies.</summary>
    public ExitPenaltyBand? BandFor(decimal yearsSinceStart)
    {
        Guard.NonNegative(yearsSinceStart);
        foreach (var band in Bands)
        {
            if (band.AppliesAt(yearsSinceStart))
            {
                return band;
            }
        }

        return null;
    }

    /// <summary>Penalty in pounds on transferring <paramref name="value"/> after <paramref name="yearsSinceStart"/> years, capped at the value.</summary>
    public decimal PenaltyFor(decimal yearsSinceStart, decimal value)
    {
        Guard.NonNegative(value);
        var band = BandFor(yearsSinceStart);
        if (band is null)
        {
            return 0m;
        }

        var penalty = value * band.Rate + band.Amount;
        return Math.Min(penalty, value);
    }

    public bool Equals(ExitPenaltySchedule? other) => other is not null && Bands.SequenceEqual(other.Bands);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var band in Bands)
        {
            hash.Add(band);
        }

        return hash.ToHashCode();
    }
}
