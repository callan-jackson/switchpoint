using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>Fund charge basis: an explicit OCF, the weighted OCF of the holdings, or none.</summary>
public sealed record FundChargeBasis
{
    private FundChargeBasis(FundChargeBasisKind kind, decimal? ocf)
    {
        Kind = kind;
        Ocf = ocf;
    }

    public FundChargeBasisKind Kind { get; }

    /// <summary>Explicit ongoing charges figure as a fraction; set only when <see cref="Kind"/> is <see cref="FundChargeBasisKind.Explicit"/>.</summary>
    public decimal? Ocf { get; }

    public static FundChargeBasis None { get; } = new(FundChargeBasisKind.None, null);

    public static FundChargeBasis FromHoldings { get; } = new(FundChargeBasisKind.FromHoldings, null);

    public static FundChargeBasis Explicit(decimal ocf) => new(FundChargeBasisKind.Explicit, Guard.Fraction(ocf));

    /// <summary>
    /// The OCF to use. <see cref="FundChargeBasisKind.FromHoldings"/> requires <paramref name="weightedOcf"/>
    /// and throws <see cref="DomainException"/> without it; the other kinds ignore it.
    /// </summary>
    public decimal ResolveOcf(decimal? weightedOcf)
    {
        switch (Kind)
        {
            case FundChargeBasisKind.None:
                return 0m;
            case FundChargeBasisKind.Explicit:
                return Ocf!.Value;
            case FundChargeBasisKind.FromHoldings:
                Guard.Against(weightedOcf is null, "The fund charge basis is FromHoldings but no weighted OCF was supplied.");
                return Guard.Fraction(weightedOcf!.Value, nameof(weightedOcf));
            default:
                throw new DomainException($"Unknown fund charge basis '{Kind}'.");
        }
    }
}
