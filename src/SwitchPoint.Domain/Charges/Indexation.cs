using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>Indexation rule for a <see cref="FixedCharge"/>: none, in line with CPI, or at a fixed annual rate.</summary>
public sealed record Indexation
{
    private Indexation(IndexationBasis basis, decimal rate)
    {
        Basis = basis;
        Rate = rate;
    }

    public IndexationBasis Basis { get; }

    /// <summary>Annual increase as a fraction; only meaningful when <see cref="Basis"/> is <see cref="IndexationBasis.Fixed"/>.</summary>
    public decimal Rate { get; }

    public static Indexation None { get; } = new(IndexationBasis.None, 0m);

    public static Indexation Cpi { get; } = new(IndexationBasis.Cpi, 0m);

    public static Indexation Fixed(decimal rate) => new(IndexationBasis.Fixed, Guard.InRange(rate, 0m, 0.5m));

    /// <summary>Multiplier applied in plan year <paramref name="year"/> (1-based; year 1 is never indexed).</summary>
    public decimal FactorForYear(int year, decimal cpiAssumption)
    {
        Guard.Positive(year);
        return Basis switch
        {
            IndexationBasis.None => 1m,
            IndexationBasis.Cpi => Arithmetic.CompoundFactor(cpiAssumption, year - 1),
            IndexationBasis.Fixed => Arithmetic.CompoundFactor(Rate, year - 1),
            _ => throw new DomainException($"Unknown indexation basis '{Basis}'."),
        };
    }
}
