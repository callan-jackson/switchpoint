using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Market;

/// <summary>Fractions of a fund or portfolio held in each broad asset class; must sum to 1.</summary>
public sealed record AssetAllocation
{
    private const decimal Tolerance = 0.000001m;

    public AssetAllocation(decimal equity, decimal fixedInterest, decimal property, decimal cash, decimal alternatives)
    {
        Equity = Guard.Fraction(equity);
        FixedInterest = Guard.Fraction(fixedInterest);
        Property = Guard.Fraction(property);
        Cash = Guard.Fraction(cash);
        Alternatives = Guard.Fraction(alternatives);
        decimal total = Equity + FixedInterest + Property + Cash + Alternatives;
        Guard.Against(Math.Abs(total - 1m) > Tolerance, $"Asset allocation must sum to 1 (got {total}).");
    }

    public decimal Equity { get; }
    public decimal FixedInterest { get; }
    public decimal Property { get; }
    public decimal Cash { get; }
    public decimal Alternatives { get; }

    public static AssetAllocation AllEquity { get; } = new(1m, 0m, 0m, 0m, 0m);
    public static AssetAllocation AllCash { get; } = new(0m, 0m, 0m, 1m, 0m);

    /// <summary>Weighted blend of several allocations (weights need not sum to 1; they are normalised).</summary>
    public static AssetAllocation Blend(IEnumerable<(AssetAllocation Allocation, decimal Weight)> parts)
    {
        decimal e = 0m, f = 0m, p = 0m, c = 0m, a = 0m, w = 0m;
        foreach ((AssetAllocation alloc, decimal weight) in parts)
        {
            Guard.NonNegative(weight);
            e += alloc.Equity * weight;
            f += alloc.FixedInterest * weight;
            p += alloc.Property * weight;
            c += alloc.Cash * weight;
            a += alloc.Alternatives * weight;
            w += weight;
        }

        Guard.Against(w == 0m, "Cannot blend allocations with zero total weight.");
        // Normalise and push any rounding residue into cash so the result always sums to exactly 1.
        e /= w;
        f /= w;
        p /= w;
        a /= w;
        c = 1m - e - f - p - a;
        return new AssetAllocation(e, f, p, c, a);
    }
}
