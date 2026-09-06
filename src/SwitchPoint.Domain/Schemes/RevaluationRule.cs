using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Schemes;

/// <summary>How a deferred DB tranche is revalued between leaving and normal retirement age.</summary>
public sealed record RevaluationRule
{
    private RevaluationRule(IndexBasis basis, decimal rate, decimal? cap, decimal? floor)
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

    /// <summary>Fixed annual rate (used only when <see cref="Basis"/> is <see cref="IndexBasis.Fixed"/>).</summary>
    public decimal Rate { get; }

    public decimal? Cap { get; }
    public decimal? Floor { get; }

    public static RevaluationRule None { get; } = new(IndexBasis.None, 0m, null, null);
    public static RevaluationRule Fixed(decimal rate) => new(IndexBasis.Fixed, rate, null, null);
    public static RevaluationRule Cpi(decimal? cap = null, decimal? floor = null) => new(cap is null && floor is null ? IndexBasis.Cpi : IndexBasis.LpiCpi, 0m, cap, floor);
    public static RevaluationRule Rpi(decimal? cap = null, decimal? floor = null) => new(cap is null && floor is null ? IndexBasis.Rpi : IndexBasis.LpiRpi, 0m, cap, floor);
    public static RevaluationRule LpiCappedCpi(decimal cap) => new(IndexBasis.LpiCpi, 0m, cap, null);
    public static RevaluationRule LpiCappedRpi(decimal cap) => new(IndexBasis.LpiRpi, 0m, cap, null);
    public static RevaluationRule Section148 { get; } = new(IndexBasis.Section148, 0m, null, null);

    /// <summary>
    /// Statutory revaluation for non-GMP benefits: LPI capped at 5% for service to 5 April 2009,
    /// 2.5% thereafter (CPI-based since 2011).
    /// </summary>
    public static RevaluationRule StatutoryPre2009 { get; } = LpiCappedCpi(0.05m);
    public static RevaluationRule StatutoryPost2009 { get; } = LpiCappedCpi(0.025m);

    /// <summary>Resolves the annual rate given the index assumptions (COBS 19 Annex 4C 1R(4) in a TVC).</summary>
    public decimal AnnualRate(decimal cpiAssumption, decimal rpiAssumption, decimal earningsAssumption)
    {
        decimal raw = Basis switch
        {
            IndexBasis.None => 0m,
            IndexBasis.Fixed => Rate,
            IndexBasis.Cpi or IndexBasis.LpiCpi => cpiAssumption,
            IndexBasis.Rpi or IndexBasis.LpiRpi => rpiAssumption,
            IndexBasis.Section148 => earningsAssumption,
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
