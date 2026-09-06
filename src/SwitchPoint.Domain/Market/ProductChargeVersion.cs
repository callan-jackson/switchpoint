using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Market;

/// <summary>A dated version of a product's charge schedule. Analyses pin the version they used.</summary>
public sealed record ProductChargeVersion
{
    public ProductChargeVersion(int version, ChargeSchedule charges, DateOnly effectiveFrom, DateOnly? effectiveTo, string? sourceUrl, DateOnly asAt, DataQuality dataQuality)
    {
        Version = Guard.Positive(version);
        Charges = Guard.NotNull(charges);
        Guard.Against(effectiveTo is { } to && to < effectiveFrom, "A charge version cannot end before it starts.");
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        SourceUrl = sourceUrl;
        AsAt = asAt;
        DataQuality = Guard.Defined(dataQuality);
    }

    public int Version { get; }
    public ChargeSchedule Charges { get; }
    public DateOnly EffectiveFrom { get; }
    public DateOnly? EffectiveTo { get; }
    public string? SourceUrl { get; }
    public DateOnly AsAt { get; }
    public DataQuality DataQuality { get; }

    public bool IsEffectiveOn(DateOnly date) => date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    /// <summary>Returns a copy of this version closed on <paramref name="effectiveTo"/>.</summary>
    public ProductChargeVersion ClosedOn(DateOnly effectiveTo) => new(Version, Charges, EffectiveFrom, effectiveTo, SourceUrl, AsAt, DataQuality);
}
