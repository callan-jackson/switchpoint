using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Schemes;

/// <summary>Valuable features of an existing arrangement that a switch would give up (FSA 2009 template outcome 2).</summary>
public sealed record Guarantees
{
    public Guarantees(
        decimal? guaranteedAnnuityRate = null,
        decimal? guaranteedGrowthRate = null,
        decimal? protectedTaxFreeCashFraction = null,
        int? protectedPensionAge = null,
        bool withProfits = false,
        decimal marketValueReductionRate = 0m,
        decimal terminalBonus = 0m,
        decimal loyaltyBonusRate = 0m)
    {
        if (guaranteedAnnuityRate is { } gar)
        {
            Guard.InRange(gar, 0m, 0.25m, nameof(guaranteedAnnuityRate));
        }

        if (guaranteedGrowthRate is { } ggr)
        {
            Guard.InRange(ggr, 0m, 0.15m, nameof(guaranteedGrowthRate));
        }

        if (protectedTaxFreeCashFraction is { } tfc)
        {
            Guard.InRange(tfc, 0.25m, 1m, nameof(protectedTaxFreeCashFraction));
        }

        if (protectedPensionAge is { } age)
        {
            Guard.InRange(age, 50, 57, nameof(protectedPensionAge));
        }

        GuaranteedAnnuityRate = guaranteedAnnuityRate;
        GuaranteedGrowthRate = guaranteedGrowthRate;
        ProtectedTaxFreeCashFraction = protectedTaxFreeCashFraction;
        ProtectedPensionAge = protectedPensionAge;
        WithProfits = withProfits;
        MarketValueReductionRate = Guard.Fraction(marketValueReductionRate);
        TerminalBonus = Guard.NonNegative(terminalBonus);
        LoyaltyBonusRate = Guard.Fraction(loyaltyBonusRate);
    }

    /// <summary>Annual income per £1 of fund guaranteed at retirement (e.g. 0.09m = 9%).</summary>
    public decimal? GuaranteedAnnuityRate { get; }

    public decimal? GuaranteedGrowthRate { get; }

    /// <summary>Protected tax-free cash entitlement above the standard 25%.</summary>
    public decimal? ProtectedTaxFreeCashFraction { get; }

    public int? ProtectedPensionAge { get; }
    public bool WithProfits { get; }

    /// <summary>Market value reduction applied on transfer from a with-profits fund.</summary>
    public decimal MarketValueReductionRate { get; }

    public decimal TerminalBonus { get; }
    public decimal LoyaltyBonusRate { get; }

    public static Guarantees None { get; } = new();

    /// <summary>True when any feature would be lost on transfer and the analysis must refer rather than auto-verdict.</summary>
    public bool RequiresReferral => GuaranteedAnnuityRate is > 0m || GuaranteedGrowthRate is > 0m
        || ProtectedTaxFreeCashFraction is > 0.25m || ProtectedPensionAge is not null || WithProfits;
}
