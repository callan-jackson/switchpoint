using SwitchPoint.Application.Dtos;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Application.Mapping;

/// <summary>Percentage ↔ fraction helpers. DTO fields ending in Pct are percentages; the domain uses fractions.</summary>
public static class Pct
{
    public static decimal ToFraction(decimal pct) => pct / 100m;

    public static decimal? ToFraction(decimal? pct) => pct is { } p ? p / 100m : null;

    public static decimal FromFraction(decimal fraction) => fraction * 100m;

    public static decimal? FromFraction(decimal? fraction) => fraction is { } f ? f * 100m : null;
}

/// <summary>Bidirectional mapping of the charge model and scheme value objects.</summary>
public static class ChargeMapping
{
    public static ChargeSchedule ToDomain(this ChargeScheduleDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ChargeSchedule
        {
            PlatformCharge = dto.PlatformCharge?.ToDomain(),
            ProductCharge = dto.ProductCharge?.ToDomain(),
            FixedCharges = [.. dto.FixedCharges.Select(f => new FixedCharge(f.Amount, f.Frequency, f.Indexation.ToDomain(), f.AppliesTo, f.Description))],
            FundCharge = dto.FundCharge.Kind switch
            {
                FundChargeBasisKind.None => FundChargeBasis.None,
                FundChargeBasisKind.FromHoldings => FundChargeBasis.FromHoldings,
                FundChargeBasisKind.Explicit => FundChargeBasis.Explicit(Pct.ToFraction(dto.FundCharge.OcfPct ?? 0m)),
                _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.FundCharge.Kind, "Unknown fund charge basis."),
            },
            TransactionCosts = Pct.ToFraction(dto.TransactionCostsPct),
            AdviserCharges = dto.AdviserCharges.ToDomain(),
            DealingCharges = new DealingCharges(dto.DealingCharges.FundDealAmount, dto.DealingCharges.EtfDealAmount, dto.DealingCharges.ExpectedFundDealsPerYear, dto.DealingCharges.ExpectedEtfDealsPerYear),
            SwitchCharge = new SwitchCharge(dto.SwitchCharge.AmountPerSwitch, dto.SwitchCharge.ExpectedSwitchesPerYear),
            ExitPenalty = dto.ExitPenalty.Bands.Count == 0
                ? ExitPenaltySchedule.None
                : new ExitPenaltySchedule(dto.ExitPenalty.Bands.Select(b => new ExitPenaltyBand(b.UntilYearsFromStart, Pct.ToFraction(b.RatePct), b.Amount))),
            BidOfferSpread = Pct.ToFraction(dto.BidOfferSpreadPct),
            AllocationRate = Pct.ToFraction(dto.AllocationRatePct),
            LargeFundDiscounts = [.. dto.LargeFundDiscounts.Select(d => new LargeFundDiscount(d.Threshold, Pct.ToFraction(d.RebateRatePct)))],
        };
    }

    public static ChargeScheduleDto ToDto(this ChargeSchedule c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new ChargeScheduleDto
        {
            PlatformCharge = c.PlatformCharge?.ToDto(),
            ProductCharge = c.ProductCharge?.ToDto(),
            FixedCharges = [.. c.FixedCharges.Select(f => new FixedChargeDto(f.Amount, f.Frequency, new IndexationDto(f.Indexation.Basis, Pct.FromFraction(f.Indexation.Rate)), f.AppliesTo, f.Description))],
            FundCharge = new FundChargeDto(c.FundCharge.Kind, Pct.FromFraction(c.FundCharge.Ocf)),
            TransactionCostsPct = Pct.FromFraction(c.TransactionCosts),
            AdviserCharges = c.AdviserCharges.ToDto(),
            DealingCharges = new DealingChargesDto(c.DealingCharges.FundDealAmount, c.DealingCharges.EtfDealAmount, c.DealingCharges.ExpectedFundDealsPerYear, c.DealingCharges.ExpectedEtfDealsPerYear),
            SwitchCharge = new SwitchChargeDto(c.SwitchCharge.AmountPerSwitch, c.SwitchCharge.ExpectedSwitchesPerYear),
            ExitPenalty = new ExitPenaltyDto([.. c.ExitPenalty.Bands.Select(b => new ExitPenaltyBandDto(b.UntilYearsFromStart, Pct.FromFraction(b.Rate), b.Amount))]),
            BidOfferSpreadPct = Pct.FromFraction(c.BidOfferSpread),
            AllocationRatePct = Pct.FromFraction(c.AllocationRate),
            LargeFundDiscounts = [.. c.LargeFundDiscounts.Select(d => new LargeFundDiscountDto(d.Threshold, Pct.FromFraction(d.RebateRate)))],
        };
    }

    public static TieredCharge ToDomain(this TieredChargeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new TieredCharge(dto.Bands.Select(b => new TierBand(b.UpTo, Pct.ToFraction(b.AnnualRatePct))), dto.Mode);
    }

    public static TieredChargeDto ToDto(this TieredCharge t)
    {
        ArgumentNullException.ThrowIfNull(t);
        return new TieredChargeDto(t.Mode, [.. t.Bands.Select(b => new TierBandDto(b.UpTo, Pct.FromFraction(b.AnnualRate)))]);
    }

    public static Indexation ToDomain(this IndexationDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Basis switch
        {
            IndexationBasis.None => Indexation.None,
            IndexationBasis.Cpi => Indexation.Cpi,
            IndexationBasis.Fixed => Indexation.Fixed(Pct.ToFraction(dto.RatePct)),
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.Basis, "Unknown indexation basis."),
        };
    }

    public static AdviserCharge ToDomain(this AdviserChargeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new AdviserCharge(Pct.ToFraction(dto.InitialPct), dto.InitialAmount, Pct.ToFraction(dto.OngoingPct), dto.OngoingAmount);
    }

    public static AdviserChargeDto ToDto(this AdviserCharge a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new AdviserChargeDto(Pct.FromFraction(a.InitialRate), a.InitialAmount, Pct.FromFraction(a.OngoingRate), a.OngoingAmount);
    }

    public static Guarantees ToDomain(this GuaranteesDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new Guarantees(
            Pct.ToFraction(dto.GuaranteedAnnuityRatePct), Pct.ToFraction(dto.GuaranteedGrowthRatePct), Pct.ToFraction(dto.ProtectedTaxFreeCashPct),
            dto.ProtectedPensionAge, dto.WithProfits, Pct.ToFraction(dto.MarketValueReductionPct), dto.TerminalBonus, Pct.ToFraction(dto.LoyaltyBonusPct));
    }

    public static GuaranteesDto ToDto(this Guarantees g)
    {
        ArgumentNullException.ThrowIfNull(g);
        return new GuaranteesDto(
            Pct.FromFraction(g.GuaranteedAnnuityRate), Pct.FromFraction(g.GuaranteedGrowthRate), Pct.FromFraction(g.ProtectedTaxFreeCashFraction),
            g.ProtectedPensionAge, g.WithProfits, Pct.FromFraction(g.MarketValueReductionRate), g.TerminalBonus, Pct.FromFraction(g.LoyaltyBonusRate));
    }

    public static Contribution ToDomain(this ContributionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new Contribution(dto.Payer, dto.Amount, dto.Frequency, Pct.ToFraction(dto.EscalationPct), dto.IsGrossOfTaxRelief, dto.StartMonth, dto.EndMonth);
    }

    public static ContributionDto ToDto(this Contribution c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new ContributionDto(c.Payer, c.Amount, c.Frequency, Pct.FromFraction(c.EscalationRate), c.IsGrossOfTaxRelief, c.StartMonth, c.EndMonth);
    }

    public static Holding ToDomain(this HoldingDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new Holding(dto.Name, Pct.ToFraction(dto.WeightPct), dto.Isin, dto.FundId, Pct.ToFraction(dto.OcfPct));
    }

    public static HoldingDto ToDto(this Holding h)
    {
        ArgumentNullException.ThrowIfNull(h);
        return new HoldingDto(h.Name, Pct.FromFraction(h.Weight), h.Isin, h.FundId, Pct.FromFraction(h.Ocf));
    }

    public static RevaluationRule ToRevaluation(this IndexRuleDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        decimal? cap = Pct.ToFraction(dto.CapPct);
        decimal? floor = Pct.ToFraction(dto.FloorPct);
        return dto.Basis switch
        {
            IndexBasis.None => RevaluationRule.None,
            IndexBasis.Fixed => RevaluationRule.Fixed(Pct.ToFraction(dto.RatePct)),
            IndexBasis.Cpi or IndexBasis.LpiCpi => RevaluationRule.Cpi(cap, floor),
            IndexBasis.Rpi or IndexBasis.LpiRpi => RevaluationRule.Rpi(cap, floor),
            IndexBasis.Section148 => RevaluationRule.Section148,
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.Basis, "Unknown index basis."),
        };
    }

    public static EscalationRule ToEscalation(this IndexRuleDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        decimal? cap = Pct.ToFraction(dto.CapPct);
        decimal? floor = Pct.ToFraction(dto.FloorPct);
        return dto.Basis switch
        {
            IndexBasis.None => EscalationRule.None,
            IndexBasis.Fixed => EscalationRule.Fixed(Pct.ToFraction(dto.RatePct)),
            IndexBasis.Cpi or IndexBasis.LpiCpi or IndexBasis.Section148 => EscalationRule.Cpi(cap, floor),
            IndexBasis.Rpi or IndexBasis.LpiRpi => EscalationRule.Rpi(cap, floor),
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.Basis, "Unknown index basis."),
        };
    }

    public static IndexRuleDto ToDto(this RevaluationRule r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new IndexRuleDto(r.Basis, Pct.FromFraction(r.Rate), Pct.FromFraction(r.Cap), Pct.FromFraction(r.Floor));
    }

    public static IndexRuleDto ToDto(this EscalationRule e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new IndexRuleDto(e.Basis, Pct.FromFraction(e.Rate), Pct.FromFraction(e.Cap), Pct.FromFraction(e.Floor));
    }

    public static ContributionSpec ToSpec(this ContributionDto dto) => ContributionSpec.From(dto.ToDomain());

    public static ChargeTotalsDto ToDto(this ChargeTotals t)
    {
        ArgumentNullException.ThrowIfNull(t);
        return new ChargeTotalsDto(t.Platform, t.Product, t.Fund, t.Transaction, t.AdviserInitial, t.AdviserOngoing, t.Fixed, t.Dealing, t.Switch, t.Discounts, t.AllocationAndSpread, t.ExitPenalty, t.Total);
    }
}
