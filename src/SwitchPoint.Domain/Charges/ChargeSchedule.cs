using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Charges;

/// <summary>
/// Everything a scheme or product charges, as an immutable value object. Every component is optional;
/// <see cref="None"/> charges nothing. Rates are fractions; amounts are pounds. Use object initialiser
/// or <c>with</c> syntax; each property validates on assignment.
/// </summary>
public sealed record ChargeSchedule
{
    private readonly IReadOnlyList<FixedCharge> _fixedCharges = [];
    private readonly IReadOnlyList<LargeFundDiscount> _largeFundDiscounts = [];
    private readonly FundChargeBasis _fundCharge = FundChargeBasis.None;
    private readonly AdviserCharge _adviserCharges = AdviserCharge.None;
    private readonly DealingCharges _dealingCharges = DealingCharges.None;
    private readonly SwitchCharge _switchCharge = SwitchCharge.None;
    private readonly ExitPenaltySchedule _exitPenalty = ExitPenaltySchedule.None;
    private readonly decimal _transactionCosts;
    private readonly decimal _bidOfferSpread;
    private readonly decimal _allocationRate = 1m;

    public static ChargeSchedule None { get; } = new();

    /// <summary>Annual platform charge tiered by fund value.</summary>
    public TieredCharge? PlatformCharge { get; init; }

    /// <summary>Annual product charge (e.g. an insurer's AMC), also tiered.</summary>
    public TieredCharge? ProductCharge { get; init; }

    public IReadOnlyList<FixedCharge> FixedCharges
    {
        get => _fixedCharges;
        init => _fixedCharges = Guard.NotNull(value).ToArray();
    }

    public FundChargeBasis FundCharge
    {
        get => _fundCharge;
        init => _fundCharge = Guard.NotNull(value);
    }

    /// <summary>Annual portfolio transaction costs as a fraction of fund value.</summary>
    public decimal TransactionCosts
    {
        get => _transactionCosts;
        init => _transactionCosts = Guard.Fraction(value);
    }

    public AdviserCharge AdviserCharges
    {
        get => _adviserCharges;
        init => _adviserCharges = Guard.NotNull(value);
    }

    public DealingCharges DealingCharges
    {
        get => _dealingCharges;
        init => _dealingCharges = Guard.NotNull(value);
    }

    public SwitchCharge SwitchCharge
    {
        get => _switchCharge;
        init => _switchCharge = Guard.NotNull(value);
    }

    public ExitPenaltySchedule ExitPenalty
    {
        get => _exitPenalty;
        init => _exitPenalty = Guard.NotNull(value);
    }

    /// <summary>Legacy-plan bid/offer spread as a fraction of each contribution (default 0).</summary>
    public decimal BidOfferSpread
    {
        get => _bidOfferSpread;
        init => _bidOfferSpread = Guard.Fraction(value);
    }

    /// <summary>Legacy-plan allocation rate: fraction of each contribution invested (default 1; may exceed 1 for enhanced allocation).</summary>
    public decimal AllocationRate
    {
        get => _allocationRate;
        init => _allocationRate = Guard.InRange(value, 0.5m, 1.5m);
    }

    /// <summary>Rebates by fund-value threshold, ascending; the highest threshold reached applies (not cumulative).</summary>
    public IReadOnlyList<LargeFundDiscount> LargeFundDiscounts
    {
        get => _largeFundDiscounts;
        init
        {
            var list = Guard.NotNull(value).ToArray();
            for (var i = 1; i < list.Length; i++)
            {
                Guard.Against(list[i].Threshold <= list[i - 1].Threshold, "Large fund discount thresholds must be strictly ascending.");
            }

            _largeFundDiscounts = list;
        }
    }

    public bool HasExitPenalty => !ExitPenalty.IsNone;

    /// <summary>The discount whose threshold is the highest one reached by <paramref name="fundValue"/>, or null.</summary>
    public LargeFundDiscount? DiscountFor(decimal fundValue)
    {
        Guard.NonNegative(fundValue);
        LargeFundDiscount? best = null;
        foreach (var discount in LargeFundDiscounts)
        {
            if (discount.AppliesTo(fundValue))
            {
                best = discount;
            }
        }

        return best;
    }

    /// <summary>
    /// Annual charges by component for plan year <paramref name="year"/> at <paramref name="fundValue"/>.
    /// <paramref name="weightedOcf"/> is required when <see cref="FundCharge"/> is FromHoldings.
    /// <paramref name="chargeInflation"/> indexes CPI-linked fixed charges; drawdown-only fixed charges
    /// count only when <paramref name="inDrawdown"/> is true. The initial adviser charge and exit
    /// penalties are one-off and are not part of the annual breakdown.
    /// </summary>
    public ChargeBreakdown BreakdownFor(decimal fundValue, int year = 1, decimal? weightedOcf = null, decimal chargeInflation = 0m, bool inDrawdown = false)
    {
        Guard.NonNegative(fundValue);
        Guard.Positive(year);

        var platform = PlatformCharge?.AnnualChargeFor(fundValue) ?? 0m;
        var product = ProductCharge?.AnnualChargeFor(fundValue) ?? 0m;
        var fund = fundValue * FundCharge.ResolveOcf(weightedOcf);
        var transaction = fundValue * TransactionCosts;
        var adviser = AdviserCharges.OngoingAnnualFor(fundValue);

        var fixedTotal = 0m;
        foreach (var charge in FixedCharges)
        {
            if (charge.AppliesTo == FixedChargeScope.Drawdown && !inDrawdown)
            {
                continue;
            }

            fixedTotal += charge.AmountInYear(year, chargeInflation);
        }

        var discount = DiscountFor(fundValue) is { } rebate ? -(fundValue * rebate.RebateRate) : 0m;

        return new ChargeBreakdown(
            fundValue,
            year,
            platform,
            product,
            fund,
            transaction,
            adviser,
            fixedTotal,
            DealingCharges.AnnualCost,
            SwitchCharge.AnnualCost,
            discount);
    }

    /// <summary>
    /// First-year total annual charge as a fraction of <paramref name="fundValue"/> (which must be positive),
    /// including fixed and dealing costs expressed against the fund and net of any large-fund discount.
    /// </summary>
    public decimal EffectiveAnnualPercentageCharge(decimal fundValue, decimal? weightedOcf = null)
    {
        Guard.Positive(fundValue);
        return BreakdownFor(fundValue, 1, weightedOcf).Total / fundValue;
    }

    public bool Equals(ChargeSchedule? other)
        => other is not null
           && Equals(PlatformCharge, other.PlatformCharge)
           && Equals(ProductCharge, other.ProductCharge)
           && FixedCharges.SequenceEqual(other.FixedCharges)
           && FundCharge == other.FundCharge
           && TransactionCosts == other.TransactionCosts
           && AdviserCharges == other.AdviserCharges
           && DealingCharges == other.DealingCharges
           && SwitchCharge == other.SwitchCharge
           && ExitPenalty == other.ExitPenalty
           && BidOfferSpread == other.BidOfferSpread
           && AllocationRate == other.AllocationRate
           && LargeFundDiscounts.SequenceEqual(other.LargeFundDiscounts);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PlatformCharge);
        hash.Add(ProductCharge);
        hash.Add(FundCharge);
        hash.Add(TransactionCosts);
        hash.Add(AdviserCharges);
        hash.Add(DealingCharges);
        hash.Add(SwitchCharge);
        hash.Add(ExitPenalty);
        hash.Add(BidOfferSpread);
        hash.Add(AllocationRate);
        foreach (var charge in FixedCharges)
        {
            hash.Add(charge);
        }

        foreach (var discount in LargeFundDiscounts)
        {
            hash.Add(discount);
        }

        return hash.ToHashCode();
    }
}
