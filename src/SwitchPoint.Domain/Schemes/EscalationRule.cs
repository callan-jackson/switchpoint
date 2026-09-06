using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Schemes;

/// <summary>How a DB pension increases once in payment.</summary>
public sealed record EscalationRule
{
    private EscalationRule(IndexBasis basis, decimal rate, decimal? cap, decimal? floor)
    {
        Basis = Guard.Defined(basis);
        Rate = Guard.InRange(rate, 0m, 0.15m);
        if (cap is { } c)
        {
            Guard.InRange(c, 0m, 0.15m, nameof(cap));
        }

        if (floor is { } f)
        {
            Guard.InRange(f, 0m, 0.15m, nameof(floor));
            Guard.Against(cap is { } cc && f > cc, "The floor cannot exceed the cap.");
        }

        Cap = cap;
        Floor = floor;
    }

    public IndexBasis Basis { get; }
    public decimal Rate { get; }
    public decimal? Cap { get; }
    public decimal? Floor { get; }

    public static EscalationRule None { get; } = new(IndexBasis.None, 0m, null, null);
    public static EscalationRule Fixed(decimal rate) => new(IndexBasis.Fixed, rate, null, null);
    public static EscalationRule Cpi(decimal? cap = null, decimal? floor = null) => new(cap is null && floor is null ? IndexBasis.Cpi : IndexBasis.LpiCpi, 0m, cap, floor);
    public static EscalationRule Rpi(decimal? cap = null, decimal? floor = null) => new(cap is null && floor is null ? IndexBasis.Rpi : IndexBasis.LpiRpi, 0m, cap, floor);
    public static EscalationRule LpiCappedCpi(decimal cap) => new(IndexBasis.LpiCpi, 0m, cap, null);
    public static EscalationRule LpiCappedRpi(decimal cap) => new(IndexBasis.LpiRpi, 0m, cap, null);

    /// <summary>Statutory minimum increases in payment: LPI 5% for pre-2005 accrual, LPI 2.5% after.</summary>
    public static EscalationRule StatutoryPre2005 { get; } = LpiCappedCpi(0.05m);
    public static EscalationRule StatutoryPost2005 { get; } = LpiCappedCpi(0.025m);

    public decimal AnnualRate(decimal cpiAssumption, decimal rpiAssumption)
    {
        decimal raw = Basis switch
        {
            IndexBasis.None => 0m,
            IndexBasis.Fixed => Rate,
            IndexBasis.Cpi or IndexBasis.LpiCpi => cpiAssumption,
            IndexBasis.Rpi or IndexBasis.LpiRpi => rpiAssumption,
            IndexBasis.Section148 => cpiAssumption,
            _ => throw new DomainException($"Unknown index basis '{Basis}'."),
        };

        if (Cap is { } cap && raw > cap)
        {
            raw = cap;
        }

        if (Floor is { } floor && raw < floor)
        {
            raw = floor;
        }

        return raw;
    }
}
