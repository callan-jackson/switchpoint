using SwitchPoint.Application.Dtos;
using SwitchPoint.Calculation.Cashflow;
using SwitchPoint.Calculation.CriticalYield;
using SwitchPoint.Calculation.DbTransfer;
using SwitchPoint.Calculation.MonteCarlo;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Calculation.Riy;
using SwitchPoint.Calculation.Tax;

namespace SwitchPoint.Application.Mapping;

/// <summary>Engine results → result DTOs (fractions → percentages).</summary>
public static class ResultMapping
{
    public static RiyDto ToDto(this RiyResult r, bool realTerms)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new RiyDto(
            Pct.FromFraction(r.GrowthRate), Pct.FromFraction(r.ProductRiy), Pct.FromFraction(r.TotalRiy),
            Pct.FromFraction(r.RateAfterProductCharges), Pct.FromFraction(r.RateAfterAllCharges),
            r.ValueBeforeCharges, r.ValueAfterProductCharges, r.ValueAfterAllCharges,
            r.ProductSentence(realTerms), r.TotalSentence(realTerms),
            [.. r.EffectOfCharges.Select(e => new EffectOfChargesRowDto(e.Year, e.PaymentsToDate, e.BeforeCharges, e.PlanAndInvestmentChargesOnly, e.AfterAllCharges, e.EffectOfDeductionsToDate))],
            r.TotalCharges.ToDto());
    }

    public static CriticalYieldAtRateDto ToDto(this CriticalYieldAtRate c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new CriticalYieldAtRateDto(Pct.FromFraction(c.GrowthRate), c.ExistingValueAtRetirement, c.ReceivingValueAtRetirement, Pct.FromFraction(c.CriticalYield), Pct.FromFraction(c.CriticalYieldReal), Pct.FromFraction(c.Headroom), c.ProjectedGain, c.BreakEvenYear, c.Converged);
    }

    public static PensionSwitchResultDto ToDto(this CriticalYieldResult r, IReadOnlyList<ChartPointDto> chart, bool realTerms, string engineVersion, DateTime calculatedAtUtc, AssumptionSetRefDto assumptionSet)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new PensionSwitchResultDto(
            r.TotalNetTransferValue, r.InitialAdviserCharge, r.AnyGuaranteesFlagged, r.Warnings,
            r.Lower.ToDto(), r.Intermediate.ToDto(), r.Higher.ToDto(), r.ReceivingRiy.ToDto(realTerms),
            [.. r.Schemes.Select(s => new CedingSchemeResultDto(s.Name, s.CurrentValue, s.NetTransferValue, s.ProjectedValueIfRetained, s.RiyIfRetained.ToDto(realTerms), s.RiyIfSwitched.ToDto(realTerms), Pct.FromFraction(s.CriticalYieldAlone), s.GuaranteesFlagged, s.Verdict))],
            chart, engineVersion, calculatedAtUtc, assumptionSet);
    }

    /// <summary>Yearly existing-vs-receiving values (real terms) for the chart; year 0 is today's values.</summary>
    public static IReadOnlyList<ChartPointDto> BuildChart(IReadOnlyList<ProjectionResult> retained, ProjectionResult receiving, decimal existingToday, decimal receivingToday)
    {
        ArgumentNullException.ThrowIfNull(retained);
        ArgumentNullException.ThrowIfNull(receiving);
        List<ChartPointDto> points = [new ChartPointDto(0, existingToday, receivingToday)];
        foreach (ProjectionRow row in receiving.Schedule.Where(r => r.Month % 12 == 0))
        {
            decimal existing = retained.Sum(p => p.AtYear(row.Year).ValueReal);
            points.Add(new ChartPointDto(row.Year, existing, row.ValueReal));
        }

        return points;
    }

    public static DbTransferResultDto ToDto(this DbTransferResult r, string engineVersion, DateTime calculatedAtUtc, AssumptionSetRefDto assumptionSet)
    {
        ArgumentNullException.ThrowIfNull(r);
        TransferValueComparator t = r.Tvc;
        return new DbTransferResultDto(
            new TvcDto(t.CashEquivalentTransferValue, t.EstimatedReplacementCost, t.Difference, t.RetirementAgeUsed, t.TermYears, Pct.FromFraction(t.GiltYieldUsed), Pct.FromFraction(t.DiscountRateUsed), t.AnnuityCostAtRetirement, t.PensionAtRetirement, t.Wording, t.Notes,
                [.. t.Tranches.Select(x => new RevaluedTrancheDto(x.Name, x.AccruedAnnualPension, Pct.FromFraction(x.RevaluationRate), x.YearsRevalued, x.PensionAtRetirement, Pct.FromFraction(x.EscalationInPayment), Pct.FromFraction(x.AnnuityInterestRate), x.AnnuityPricePerPound, x.AnnuityCost, x.IsGmp))]),
            new CriticalYieldsDto(Pct.FromFraction(r.CriticalYields.TypeAAnnuityMatch), Pct.FromFraction(r.CriticalYields.TypeBPclsAndReducedPension), Pct.FromFraction(r.CriticalYields.DrawdownHurdleRate), r.CriticalYields.SchemePcls, r.CriticalYields.ResidualPensionAfterPcls, r.CriticalYields.Converged),
            r.SustainableRealIncomeFromTransfer,
            [.. r.IncomeComparison.Select(i => new IncomeComparisonRowDto(i.Age, i.SchemeIncomeNominal, i.SchemeIncomeReal, i.DrawdownIncomeReal, i.ResidualFundReal, i.SchemeDeathBenefitReal))],
            [.. r.StressTests.Select(s => new StressScenarioDto(s.Name, s.SustainableRealIncome, s.Change))],
            new OnePageSummaryDto(r.Summary.InitialAdviceFee, r.Summary.RevaluedMonthlyIncome, r.Summary.PaybackMonths, r.Summary.FirstYearChargesProposed, r.Summary.OngoingAnnualChargesProposed, r.Summary.FirstYearChargesCeding, r.Summary.FirstYearChargesWorkplaceDefault),
            r.LifeExpectancyAtRetirement, r.Warnings, engineVersion, calculatedAtUtc, assumptionSet);
    }

    public static CashflowResultDto ToDto(this CashflowResult r, decimal sustainableSpend, string engineVersion, DateTime calculatedAtUtc, AssumptionSetRefDto assumptionSet)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new CashflowResultDto(
            [.. r.Rows.Select(x => new CashflowRowDto(x.Year, x.Age, x.PartnerAge, x.EmploymentIncome, x.StatePensionIncome, x.DbPensionIncome, x.OtherIncome, x.PensionWithdrawalsTaxable, x.TaxFreeCash, x.IsaWithdrawals, x.GiaWithdrawals, x.CashWithdrawals,
                x.IncomeTax, x.NationalInsurance, x.CapitalGainsTax, x.NetIncome, x.NetIncomeReal, x.Expenses, x.Surplus, x.Shortfall, x.Contributions,
                [.. x.Assets.Select(a => new AssetValueDto(a.Name, a.Kind, a.Value, a.ValueReal))], x.TotalAssets, x.TotalAssetsReal, x.Warnings))],
            r.FirstShortfallAge, r.TotalIncomeTax, r.TotalShortfall, r.LegacyAtEnd, r.LegacyAtEndReal, r.LumpSumAllowanceUsed, r.Succeeds, sustainableSpend, engineVersion, calculatedAtUtc, assumptionSet);
    }

    public static StochasticResultDto ToDto(this MonteCarloResult r, string engineVersion, DateTime calculatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new StochasticResultDto(
            r.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Paths, r.ProbabilityOfSuccess,
            [.. r.TotalAssetsReal.Select(p => new PercentileRowDto(p.Year, p.Age, p.P5, p.P10, p.P25, p.P50, p.P75, p.P90, p.P95))],
            [.. r.NetIncomeReal.Select(p => new PercentileRowDto(p.Year, p.Age, p.P5, p.P10, p.P25, p.P50, p.P75, p.P90, p.P95))],
            r.MedianShortfallAge, r.WorstDecileShortfallAge,
            new ConservativenessDto(r.Conservativeness.DeterministicAssetsAtEnd, r.Conservativeness.MedianAssetsAtEnd, r.Conservativeness.MedianIsNoLessConservative),
            r.MeanLegacyReal, engineVersion, calculatedAtUtc);
    }

    public static TaxComputationDto ToDto(this TaxComputation t)
    {
        ArgumentNullException.ThrowIfNull(t);
        return new TaxComputationDto(t.TaxYear, t.Regime, t.AdjustedNetIncome, t.PersonalAllowance, t.TaxableIncome, t.IncomeTax, t.NationalInsurance, t.TotalDeductions, Pct.FromFraction(t.MarginalRate),
            [.. t.Lines.Select(l => new TaxLineDto(l.Category, l.Band, l.Amount, Pct.FromFraction(l.Rate), l.Tax))]);
    }
}
