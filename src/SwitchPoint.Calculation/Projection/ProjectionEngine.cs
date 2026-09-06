using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Calculation.Projection;

/// <summary>A contribution stream for the projection engine.</summary>
/// <param name="Amount">Amount per payment (per month for monthly, per year for annual, the whole amount for single).</param>
/// <param name="Frequency">Payment frequency.</param>
/// <param name="EscalationRate">Annual escalation applied at each plan anniversary.</param>
/// <param name="GrossOfTaxRelief">False for a net relief-at-source amount, which is grossed up by the basic rate.</param>
/// <param name="StartMonth">First projection month (1-based) the contribution is paid.</param>
/// <param name="EndMonth">Last month paid, inclusive; null = to the end.</param>
public sealed record ContributionSpec(decimal Amount, Frequency Frequency, decimal EscalationRate = 0m, bool GrossOfTaxRelief = true, int StartMonth = 1, int? EndMonth = null)
{
    /// <summary>Converts a domain contribution.</summary>
    public static ContributionSpec From(Contribution c)
    {
        ArgumentNullException.ThrowIfNull(c);
        // Employer and third-party contributions are always gross.
        bool gross = c.IsGrossOfTaxRelief || c.Payer != ContributionPayer.Member;
        return new ContributionSpec(c.Amount, c.Frequency, c.EscalationRate, gross, c.StartMonth ?? 1, c.EndMonth);
    }

    internal bool IsDue(int month)
    {
        if (month < StartMonth || (EndMonth is { } end && month > end))
        {
            return false;
        }

        int offset = month - StartMonth;
        return Frequency switch
        {
            Frequency.Monthly => true,
            Frequency.Quarterly => offset % 3 == 0,
            Frequency.Annually => offset % 12 == 0,
            Frequency.Single => offset == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(month), Frequency, "Unknown frequency."),
        };
    }
}

/// <summary>Inputs to a single-arrangement projection. See docs/methodology/projection.md.</summary>
public sealed record ProjectionRequest
{
    public required decimal StartValue { get; init; }
    public required int Months { get; init; }

    /// <summary>Annual nominal growth of the underlying investments before charges.</summary>
    public required decimal GrowthRate { get; init; }

    public required ChargeSchedule Charges { get; init; }
    public IReadOnlyList<ContributionSpec> Contributions { get; init; } = [];

    /// <summary>Weighted OCF of the holdings, used when the schedule's fund basis is FromHoldings.</summary>
    public decimal? WeightedOcf { get; init; }

    /// <summary>Price inflation used to express values in today's money and to index CPI-linked fixed charges.</summary>
    public decimal Inflation { get; init; } = 0.02m;

    /// <summary>Rate at which CPI-indexed fixed charges grow (normally the inflation assumption).</summary>
    public decimal ChargeInflation { get; init; } = 0.02m;

    public bool InDrawdown { get; init; }
    public decimal ReliefAtSourceRate { get; init; } = 0.20m;

    /// <summary>Deduct the initial adviser charge (percentage and amount) from the start value before investing.</summary>
    public bool ApplyInitialAdviserCharge { get; init; } = true;

    /// <summary>Apply the initial adviser percentage to each contribution as well as the start value.</summary>
    public bool ApplyInitialChargeToContributions { get; init; }

    /// <summary>Apply the exit penalty schedule to the final value (used when a future transfer is modelled).</summary>
    public bool ApplyExitPenaltyAtEnd { get; init; }

    /// <summary>Years the plan had been in force at the start, for exit penalty timing.</summary>
    public decimal YearsInForceAtStart { get; init; }

    /// <summary>Other household assets on the same platform that count towards tier thresholds (family linking).</summary>
    public decimal HouseholdLinkedValue { get; init; }
}

/// <summary>Cumulative charges by category.</summary>
public sealed record ChargeTotals(
    decimal Platform, decimal Product, decimal Fund, decimal Transaction, decimal AdviserInitial, decimal AdviserOngoing,
    decimal Fixed, decimal Dealing, decimal Switch, decimal Discounts, decimal AllocationAndSpread, decimal ExitPenalty)
{
    public static ChargeTotals Zero { get; } = new(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);

    public decimal Total => Platform + Product + Fund + Transaction + AdviserInitial + AdviserOngoing + Fixed + Dealing + Switch + Discounts + AllocationAndSpread + ExitPenalty;

    /// <summary>Everything except adviser charges (COBS 13 Annex 4 "plan and investment charges").</summary>
    public decimal ProductAndInvestment => Total - AdviserInitial - AdviserOngoing;

    public ChargeTotals Add(ChargeTotals o) => new(
        Platform + o.Platform, Product + o.Product, Fund + o.Fund, Transaction + o.Transaction, AdviserInitial + o.AdviserInitial,
        AdviserOngoing + o.AdviserOngoing, Fixed + o.Fixed, Dealing + o.Dealing, Switch + o.Switch, Discounts + o.Discounts,
        AllocationAndSpread + o.AllocationAndSpread, ExitPenalty + o.ExitPenalty);
}

/// <summary>State at the end of a month (rows are emitted at each plan-year end and at the final month).</summary>
public sealed record ProjectionRow(int Month, int Year, decimal Value, decimal ValueReal, decimal ContributionsToDate, decimal GrowthToDate, ChargeTotals ChargesToDate);

/// <summary>Output of a projection.</summary>
public sealed record ProjectionResult(
    decimal FinalValue,
    decimal FinalValueReal,
    IReadOnlyList<ProjectionRow> Schedule,
    ChargeTotals TotalCharges,
    decimal TotalContributions,
    decimal TotalGrowth,
    decimal UnpaidCharges,
    decimal InitialAdviserCharge,
    decimal ExitPenaltyAtEnd)
{
    /// <summary>Row for a given plan year (1-based); the last row if the year exceeds the term.</summary>
    public ProjectionRow AtYear(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        return Schedule.FirstOrDefault(r => r.Year == year && r.Month % 12 == 0) ?? Schedule[^1];
    }
}

/// <summary>The three projections behind an "effect of charges" table (COBS 13 Annex 4 2.2R).</summary>
public sealed record ChargeEffectProjections(ProjectionResult BeforeCharges, ProjectionResult PlanAndInvestmentChargesOnly, ProjectionResult AllCharges);

/// <summary>
/// Monthly fund projection applying growth, contributions and every charge type in a <see cref="ChargeSchedule"/>.
/// Pure and deterministic; all arithmetic in decimal. See docs/methodology/projection.md.
/// </summary>
public sealed class ProjectionEngine
{
    public ProjectionResult Project(ProjectionRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Validate(r);

        ChargeSchedule c = r.Charges;
        decimal ocf = c.FundCharge.Kind == FundChargeBasisKind.None ? 0m : c.FundCharge.ResolveOcf(r.WeightedOcf);
        decimal monthlyGrowth = RateMath.AnnualToMonthly(r.GrowthRate);
        decimal deflatorMonthly = RateMath.AnnualToMonthly(r.Inflation);

        decimal value = r.StartValue;
        decimal initialAdviser = 0m;
        if (r.ApplyInitialAdviserCharge && !c.AdviserCharges.IsNone)
        {
            initialAdviser = Math.Min(value, c.AdviserCharges.InitialFor(value));
            value -= initialAdviser;
        }

        ChargeTotals totals = ChargeTotals.Zero with { AdviserInitial = initialAdviser };
        decimal contributionsToDate = 0m;
        decimal growthToDate = 0m;
        decimal unpaid = 0m;
        List<ProjectionRow> rows = [];

        for (int month = 1; month <= r.Months; month++)
        {
            int year = ((month - 1) / 12) + 1;

            // 1. Contributions received at the start of the month.
            decimal grossContribution = 0m;
            foreach (ContributionSpec spec in r.Contributions)
            {
                if (!spec.IsDue(month))
                {
                    continue;
                }

                decimal amount = spec.Amount * DecimalMath.IntegerPow(1m + spec.EscalationRate, year - 1);
                if (!spec.GrossOfTaxRelief)
                {
                    amount /= 1m - r.ReliefAtSourceRate;
                }

                grossContribution += amount;
            }

            decimal allocationLoss = 0m;
            decimal initialOnContribution = 0m;
            if (grossContribution > 0m)
            {
                contributionsToDate += grossContribution;
                decimal invested = grossContribution;
                if (r.ApplyInitialChargeToContributions && c.AdviserCharges.InitialRate > 0m)
                {
                    initialOnContribution = invested * c.AdviserCharges.InitialRate;
                    invested -= initialOnContribution;
                }

                decimal afterAllocation = invested * c.AllocationRate * (1m - c.BidOfferSpread);
                allocationLoss = invested - afterAllocation;
                value += afterAllocation;
            }

            // 2. Growth for the month.
            decimal growth = value * monthlyGrowth;
            value += growth;
            growthToDate += growth;

            // 3. Percentage charges on the month-end value before charges.
            decimal tierBasis = value + r.HouseholdLinkedValue;
            decimal platform = c.PlatformCharge is null ? 0m : (r.HouseholdLinkedValue > 0m ? c.PlatformCharge.EffectiveRateFor(tierBasis) * value : c.PlatformCharge.AnnualChargeFor(value)) / 12m;
            decimal product = c.ProductCharge is null ? 0m : c.ProductCharge.AnnualChargeFor(value) / 12m;
            decimal fund = value * ocf / 12m;
            decimal transaction = value * c.TransactionCosts / 12m;
            decimal adviserOngoing = c.AdviserCharges.OngoingAnnualFor(value) / 12m;
            decimal discount = c.DiscountFor(tierBasis) is { } rebate ? -(value * rebate.RebateRate / 12m) : 0m;

            // 4. Fixed charges due this month.
            decimal fixedDue = 0m;
            foreach (FixedCharge fc in c.FixedCharges)
            {
                if (fc.AppliesTo == FixedChargeScope.Drawdown && !r.InDrawdown)
                {
                    continue;
                }

                if (!IsFixedChargeDue(fc.Frequency, month))
                {
                    continue;
                }

                decimal annual = fc.AmountInYear(year, r.ChargeInflation);
                fixedDue += fc.Frequency == Frequency.Single ? annual : annual / fc.Frequency.PaymentsPerYear();
            }

            // 5. Dealing and switch charges spread evenly.
            decimal dealing = c.DealingCharges.AnnualCost / 12m;
            decimal switching = c.SwitchCharge.AnnualCost / 12m;

            decimal monthCharges = platform + product + fund + transaction + adviserOngoing + discount + fixedDue + dealing + switching;
            ChargeTotals monthTotals = new(platform, product, fund, transaction, initialOnContribution, adviserOngoing, fixedDue, dealing, switching, discount, allocationLoss, 0m);

            // 6. Deduct; a fund cannot go below zero.
            if (monthCharges > value)
            {
                unpaid += monthCharges - value;
                monthTotals = ScaleToAvailable(monthTotals, value, monthCharges);
                value = 0m;
            }
            else
            {
                value -= monthCharges;
            }

            totals = totals.Add(monthTotals);

            if (month % 12 == 0 || month == r.Months)
            {
                decimal deflator = DecimalMath.IntegerPow(1m + deflatorMonthly, month);
                rows.Add(new ProjectionRow(month, year, value, value / deflator, contributionsToDate, growthToDate, totals));
            }
        }

        decimal exitPenalty = 0m;
        if (r.ApplyExitPenaltyAtEnd && c.HasExitPenalty)
        {
            exitPenalty = Math.Min(value, c.ExitPenalty.PenaltyFor(r.YearsInForceAtStart + (r.Months / 12m), value));
            value -= exitPenalty;
            totals = totals with { ExitPenalty = exitPenalty };
            if (rows.Count > 0)
            {
                ProjectionRow last = rows[^1];
                decimal deflator = DecimalMath.IntegerPow(1m + deflatorMonthly, last.Month);
                rows[^1] = last with { Value = value, ValueReal = value / deflator, ChargesToDate = totals };
            }
        }

        decimal finalDeflator = DecimalMath.IntegerPow(1m + deflatorMonthly, r.Months);
        return new ProjectionResult(value, value / finalDeflator, rows, totals, contributionsToDate, growthToDate, unpaid, initialAdviser, exitPenalty);
    }

    /// <summary>Runs the same projection with all charges, with plan and investment charges only, and with none.</summary>
    public ChargeEffectProjections ProjectWithAndWithoutCharges(ProjectionRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        ProjectionRequest none = r with { Charges = ChargeSchedule.None, ApplyExitPenaltyAtEnd = false };
        ProjectionRequest productOnly = r with { Charges = r.Charges with { AdviserCharges = AdviserCharge.None } };
        return new ChargeEffectProjections(Project(none), Project(productOnly), Project(r));
    }

    private static bool IsFixedChargeDue(Frequency frequency, int month) => frequency switch
    {
        Frequency.Monthly => true,
        Frequency.Quarterly => (month - 1) % 3 == 0,
        Frequency.Annually => (month - 1) % 12 == 0,
        Frequency.Single => month == 1,
        _ => false,
    };

    private static ChargeTotals ScaleToAvailable(ChargeTotals t, decimal available, decimal demanded)
    {
        if (demanded <= 0m)
        {
            return t;
        }

        decimal f = available / demanded;
        return new ChargeTotals(t.Platform * f, t.Product * f, t.Fund * f, t.Transaction * f, t.AdviserInitial, t.AdviserOngoing * f,
            t.Fixed * f, t.Dealing * f, t.Switch * f, t.Discounts * f, t.AllocationAndSpread, 0m);
    }

    private static void Validate(ProjectionRequest r)
    {
        if (r.StartValue < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), r.StartValue, "Start value cannot be negative.");
        }

        if (r.Months < 0 || r.Months > 1200)
        {
            throw new ArgumentOutOfRangeException(nameof(r), r.Months, "Months must be between 0 and 1200.");
        }

        if (r.GrowthRate <= -1m || r.GrowthRate > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), r.GrowthRate, "Growth rate must be between -100% and 100%.");
        }

        if (r.Inflation <= -1m || r.ChargeInflation < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), "Inflation must exceed -100% and charge inflation must be non-negative.");
        }

        if (r.ReliefAtSourceRate < 0m || r.ReliefAtSourceRate >= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), r.ReliefAtSourceRate, "Relief at source rate must be in [0, 1).");
        }

        if (r.HouseholdLinkedValue < 0m || r.YearsInForceAtStart < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), "Household linked value and years in force cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(r.Charges);
        ArgumentNullException.ThrowIfNull(r.Contributions);
        foreach (ContributionSpec spec in r.Contributions)
        {
            if (spec.Amount < 0m || spec.StartMonth < 1 || (spec.EndMonth is { } e && e < spec.StartMonth) || spec.EscalationRate <= -1m)
            {
                throw new ArgumentOutOfRangeException(nameof(r), "A contribution has a negative amount, an invalid month range or an escalation at or below -100%.");
            }
        }
    }
}
