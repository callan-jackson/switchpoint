using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Domain.Charges;

namespace SwitchPoint.Calculation.Riy;

/// <summary>One row of the COBS 13 Annex 4 2.2R "effect of charges" table.</summary>
public sealed record EffectOfChargesRow(int Year, decimal PaymentsToDate, decimal BeforeCharges, decimal PlanAndInvestmentChargesOnly, decimal AfterAllCharges)
{
    /// <summary>COBS 13 Annex 3 2.2R note 5: fund without charges minus fund after all charges.</summary>
    public decimal EffectOfDeductionsToDate => BeforeCharges - AfterAllCharges;
}

/// <summary>Reduction in yield per COBS 13 Annex 4 3.1R–3.3R (unrounded; presentation rounds to 0.1%).</summary>
/// <param name="GrowthRate">B: the projection rate used.</param>
/// <param name="ProductRiy">A = B − C: product, platform and fund charges only.</param>
/// <param name="TotalRiy">D = B − E: all charges including adviser charges.</param>
/// <param name="RateAfterProductCharges">C.</param>
/// <param name="RateAfterAllCharges">E.</param>
public sealed record RiyResult(
    decimal GrowthRate,
    decimal ProductRiy,
    decimal TotalRiy,
    decimal RateAfterProductCharges,
    decimal RateAfterAllCharges,
    decimal ValueBeforeCharges,
    decimal ValueAfterProductCharges,
    decimal ValueAfterAllCharges,
    IReadOnlyList<EffectOfChargesRow> EffectOfCharges,
    ChargeTotals TotalCharges)
{
    /// <summary>Product RIY rounded to the nearest 0.1% as COBS 13 Annex 4 3.1R requires.</summary>
    public decimal ProductRiyRounded => RateMath.RoundToNearest(ProductRiy, 0.001m);

    public decimal TotalRiyRounded => RateMath.RoundToNearest(TotalRiy, 0.001m);

    /// <summary>COBS 13 Annex 4 3.3R sentence.</summary>
    public string ProductSentence(bool realTerms) =>
        $"Product charges reduce investment growth{(realTerms ? " after price inflation" : string.Empty)} from {GrowthRate:P1} to {RateMath.RoundToNearest(RateAfterProductCharges, 0.001m):P1}.";

    public string TotalSentence(bool realTerms) =>
        $"All charges reduce investment growth{(realTerms ? " after price inflation" : string.Empty)} from {GrowthRate:P1} to {RateMath.RoundToNearest(RateAfterAllCharges, 0.001m):P1}.";
}

/// <summary>
/// Reduction in yield by the COBS 13 construction: the charge-free rate that reproduces the charged projection value.
/// See docs/methodology/riy.md.
/// </summary>
public sealed class ReductionInYieldCalculator
{
    private readonly ProjectionEngine _engine;

    public ReductionInYieldCalculator(ProjectionEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>Computes RIY for the arrangement described by <paramref name="request"/> (whose GrowthRate is B).</summary>
    public RiyResult Calculate(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Months == 0)
        {
            throw new ArgumentException("RIY needs a projection term of at least one month.", nameof(request));
        }

        ChargeEffectProjections p = _engine.ProjectWithAndWithoutCharges(request);
        decimal b = request.GrowthRate;

        decimal c = SolveChargeFreeRate(request, p.PlanAndInvestmentChargesOnly.FinalValue, p.BeforeCharges.FinalValue, b);
        decimal e = SolveChargeFreeRate(request, p.AllCharges.FinalValue, p.BeforeCharges.FinalValue, b);

        List<EffectOfChargesRow> table = BuildTable(p, request.Months);
        return new RiyResult(b, b - c, b - e, c, e, p.BeforeCharges.FinalValue, p.PlanAndInvestmentChargesOnly.FinalValue, p.AllCharges.FinalValue, table, p.AllCharges.TotalCharges);
    }

    /// <summary>Years shown in the effect-of-charges table: 1, 3, 5, then every fifth year, and the final year (COBS 13 Annex 4 2.2R; Annex 3 note 1B).</summary>
    public static IReadOnlyList<int> TableYears(int months, bool drawdown = false)
    {
        int finalYear = Math.Max(1, (int)Math.Ceiling(months / 12m));
        SortedSet<int> years = [];
        if (drawdown)
        {
            for (int y = 1; y <= Math.Min(10, finalYear); y++)
            {
                years.Add(y);
            }
        }
        else
        {
            foreach (int y in new[] { 1, 3, 5 })
            {
                if (y <= finalYear)
                {
                    years.Add(y);
                }
            }

            for (int y = 10; y < finalYear; y += 5)
            {
                years.Add(y);
            }
        }

        years.Add(finalYear);
        return [.. years];
    }

    private decimal SolveChargeFreeRate(ProjectionRequest request, decimal targetValue, decimal valueAtB, decimal b)
    {
        if (targetValue >= valueAtB)
        {
            return b; // no charges (or negative net charges): nothing to reduce
        }

        if (targetValue <= 0m)
        {
            // The fund was exhausted by charges; the charge-free rate is −100% in the limit. Report the floor.
            return -0.99m;
        }

        ProjectionRequest free = request with { Charges = ChargeSchedule.None, ApplyExitPenaltyAtEnd = false };
        RootResult root = RootFinder.Solve(rate => _engine.Project(free with { GrowthRate = rate }).FinalValue - targetValue, b - 0.5m, b, RootOptions.Default);
        return root.Value;
    }

    private static List<EffectOfChargesRow> BuildTable(ChargeEffectProjections p, int months)
    {
        List<EffectOfChargesRow> rows = [];
        foreach (int year in TableYears(months))
        {
            ProjectionRow before = p.BeforeCharges.AtYear(year);
            ProjectionRow product = p.PlanAndInvestmentChargesOnly.AtYear(year);
            ProjectionRow all = p.AllCharges.AtYear(year);
            rows.Add(new EffectOfChargesRow(year, all.ContributionsToDate, before.Value, product.Value, all.Value));
        }

        return rows;
    }
}
