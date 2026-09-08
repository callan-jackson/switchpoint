using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Ports;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Reports.Charts;
using SwitchPoint.Reports.Model;

namespace SwitchPoint.Reports.Composition;

/// <summary>
/// Turns a <see cref="ReportRequest"/> into a format-independent <see cref="ReportModel"/>. Every report ends with the
/// same audit statement so a reader can tie the document to the stored analysis and its result hash.
/// </summary>
public static class ReportComposer
{
    public static ReportModel Compose(ReportRequest request, string templateVersion)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<ReportSection> sections = request.Kind switch
        {
            ReportKind.PensionSwitch => PensionSwitch(request),
            ReportKind.DbTransfer => DbTransfer(request),
            ReportKind.Cashflow => Cashflow(request),
            ReportKind.FundComparison => FundComparison(request),
            ReportKind.Suitability => Suitability(request),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unknown report kind."),
        };
        sections.Add(AuditStatement(request));

        string title = request.Kind switch
        {
            ReportKind.PensionSwitch => "Pension switching analysis",
            ReportKind.DbTransfer => "Defined benefit transfer analysis",
            ReportKind.Cashflow => "Cashflow plan",
            ReportKind.FundComparison => "Fund comparison",
            _ => "Suitability report",
        };

        ReportCover cover = new(title, request.Client.FullName,
        [
            Fmt.Kv("Client", request.Client.FullName),
            Fmt.Kv("Date of birth", Fmt.Date(request.Client.DateOfBirth)),
            Fmt.Kv("Prepared by", request.GeneratedBy),
            Fmt.Kv("Prepared on", Fmt.Stamp(request.GeneratedAtUtc)),
            Fmt.Kv("Firm", $"{request.FirmName} (FRN {request.FirmReferenceNumber})"),
            Fmt.Kv("Analysis", $"{request.AnalysisId:D} (version {request.AnalysisVersion})"),
            Fmt.Kv("Assumptions", $"{request.AssumptionSet.Name} v{request.AssumptionSet.Version}"),
        ]);

        return new ReportModel($"{request.AnalysisId:N}-v{request.AnalysisVersion}", request.FirmName, request.FirmReferenceNumber, request.Client.FullName,
            request.GeneratedBy, request.GeneratedAtUtc, templateVersion, cover, sections);
    }

    // ------------------------------------------------------------------ shared sections

    private static ReportSection Assumptions(ReportRequest r, IEnumerable<KeyValuePair<string, string>>? extra = null)
    {
        AssumptionSetDto a = r.AssumptionSet;
        List<KeyValuePair<string, string>> rows =
        [
            Fmt.Kv("Assumption set", $"{a.Name} (version {a.Version}{(a.IsFcaStandard ? ", FCA standard" : string.Empty)})"),
            Fmt.Kv("Growth rates (lower / intermediate / higher)", $"{Fmt.Pct(a.GrowthLowerPct, 1)} / {Fmt.Pct(a.GrowthIntermediatePct, 1)} / {Fmt.Pct(a.GrowthHigherPct, 1)}"),
            Fmt.Kv("Price inflation", Fmt.Pct(a.InflationPct, 1)),
            Fmt.Kv("Earnings growth", Fmt.Pct(a.EarningsGrowthPct, 1)),
            Fmt.Kv("Projection basis", a.ProjectionBasis == Domain.Assumptions.ProjectionBasis.Real ? "Real (today's money)" : "Nominal"),
            Fmt.Kv("Tax year", a.TaxYear),
            Fmt.Kv("Market data as at", Fmt.Date(a.MarketInputs.AsAt)),
        ];
        if (extra is not null)
        {
            rows.AddRange(extra);
        }

        return new SectionBuilder("Assumptions used")
            .P("Standardised projection rates follow COBS 13 Annex 2. Values shown in real terms are expressed in today's money using the price inflation assumption.")
            .Facts(rows)
            .Build();
    }

    private static ReportSection AuditStatement(ReportRequest r) =>
        new SectionBuilder("Audit statement")
            .P("This document was generated from a stored, locked analysis. The hash below covers the calculation inputs and results; any change to the analysis produces a different hash and a new report.")
            .Facts(
            [
                Fmt.Kv("Analysis identifier", request0(r)),
                Fmt.Kv("Analysis version", Fmt.Count(r.AnalysisVersion)),
                Fmt.Kv("Result hash (SHA-256)", r.AnalysisResultHash),
                Fmt.Kv("Calculation engine", "SwitchPoint 1.0.0"),
                Fmt.Kv("Generated", $"{Fmt.Stamp(r.GeneratedAtUtc)} by {r.GeneratedBy}"),
            ])
            .Build();

    private static string request0(ReportRequest r) => r.AnalysisId.ToString("D");

    private static ReportSection ClientSnapshot(ReportRequest r)
    {
        ClientDetail c = r.Client;
        return new SectionBuilder("Your circumstances")
            .Facts(
            [
                Fmt.Kv("Name", c.FullName),
                Fmt.Kv("Age", Fmt.Count(c.Age)),
                Fmt.Kv("Target retirement age", Fmt.Count(c.TargetRetirementAge)),
                Fmt.Kv("Employment", Fmt.Words(c.EmploymentStatus)),
                Fmt.Kv("Annual salary", Fmt.Money(c.AnnualSalary)),
                Fmt.Kv("Income tax regime", c.TaxRegime == Domain.Clients.TaxRegime.Scotland ? "Scotland" : "Rest of UK"),
                Fmt.Kv("Attitude to risk", $"{c.RiskProfile} of 7"),
                Fmt.Kv("State Pension", c.StatePension.ForecastWeeklyAmount is { } w ? $"{Fmt.Money2(w)} a week (forecast)" : c.StatePension.QualifyingYears is { } y ? $"{y} qualifying years" : "not supplied"),
            ])
            .Build();
    }

    // ------------------------------------------------------------------ pension switch

    private static List<ReportSection> PensionSwitch(ReportRequest r)
    {
        PensionSwitchAnalysisDto analysis = (PensionSwitchAnalysisDto)r.Analysis;
        PensionSwitchResultDto result = analysis.Result ?? throw new InvalidOperationException("The analysis has no result; calculate it before generating a report.");
        CriticalYieldAtRateDto mid = result.Intermediate;

        SectionBuilder summary = new("Executive summary");
        summary.P($"This analysis compares {result.Schemes.Count} existing arrangement{(result.Schemes.Count == 1 ? string.Empty : "s")} with the proposed plan to {Fmt.Count(analysis.RetirementAge)}, using the {r.AssumptionSet.Name} assumptions.");
        summary.Facts(
        [
            Fmt.Kv("Total transfer value", Fmt.Money(result.TotalNetTransferValue)),
            Fmt.Kv("Critical yield (intermediate rate)", Fmt.Pct(mid.CriticalYieldPct)),
            Fmt.Kv("Headroom over the assumed growth rate", Fmt.SignedPct(mid.HeadroomPct)),
            Fmt.Kv("Projected difference at retirement", Fmt.Money(mid.ProjectedGain)),
            Fmt.Kv("Break-even year", mid.BreakEvenYear is { } y ? $"year {y}" : "not reached within the term"),
            Fmt.Kv("Initial adviser charge", Fmt.Money(result.InitialAdviserCharge)),
        ]);
        summary.P(result.ReceivingRiy.ProductSentence);
        summary.P(result.ReceivingRiy.TotalSentence);
        if (result.Warnings.Count > 0)
        {
            summary.Callout(CalloutKind.Warning, "Points requiring adviser judgement", result.Warnings);
        }

        SectionBuilder verdicts = new("Each existing arrangement");
        verdicts.Table(new TableBlock(
            [
                new TableColumn("Arrangement"),
                new TableColumn("Current value", ColumnAlign.Right),
                new TableColumn("Transfer value", ColumnAlign.Right),
                new TableColumn("Projected if retained", ColumnAlign.Right),
                new TableColumn("RIY if retained", ColumnAlign.Right),
                new TableColumn("RIY if switched", ColumnAlign.Right),
                new TableColumn("Critical yield", ColumnAlign.Right),
                new TableColumn("Indication"),
            ],
            [.. result.Schemes.Select(s => new List<string>
            {
                s.Name,
                Fmt.Money(s.CurrentValue),
                Fmt.Money(s.NetTransferValue),
                Fmt.Money(s.ProjectedValueIfRetained),
                Fmt.Pct(s.RiyIfRetained.TotalRiyPct),
                Fmt.Pct(s.RiyIfSwitched.TotalRiyPct),
                Fmt.Pct(s.CriticalYieldAlonePct),
                Verdict(s.Verdict),
            })],
            Note: "Reduction in yield follows COBS 13 Annex 4 3.1R–3.2R. 'Refer' marks an arrangement with guarantees or protected features that must be considered before any switch (FSA pension switching thematic review, 2009)."));

        SectionBuilder rates = new("Critical yield at the standardised rates");
        rates.Table(new TableBlock(
            [new TableColumn("Growth rate"), new TableColumn("Existing value at retirement", ColumnAlign.Right), new TableColumn("Proposed value at retirement", ColumnAlign.Right), new TableColumn("Critical yield", ColumnAlign.Right), new TableColumn("Headroom", ColumnAlign.Right)],
            [
                RateRow("Lower", result.Lower),
                RateRow("Intermediate", result.Intermediate),
                RateRow("Higher", result.Higher),
            ],
            Note: "The critical yield is the growth rate the proposed plan must achieve, before its charges, to match the projected value of the existing arrangements."));

        if (result.Chart.Count > 1)
        {
            string svg = SvgCharts.Line(new LineChart("Projected value in today's money", "Plan year", "Fund value",
            [
                new LineSeries("Existing arrangements", [.. result.Chart.Select(p => new ChartPoint(p.Year, p.ExistingValue))], ChartPalette.Existing, Dashed: true),
                new LineSeries("Proposed plan", [.. result.Chart.Select(p => new ChartPoint(p.Year, p.ReceivingValue))], ChartPalette.Proposed),
            ]));
            TableBlock data = new(
                [new TableColumn("Year", ColumnAlign.Right), new TableColumn("Existing", ColumnAlign.Right), new TableColumn("Proposed", ColumnAlign.Right)],
                [.. result.Chart.Where((_, i) => i % Math.Max(1, result.Chart.Count / 12) == 0 || i == result.Chart.Count - 1).Select(p => new List<string> { Fmt.Count(p.Year), Fmt.Money(p.ExistingValue), Fmt.Money(p.ReceivingValue) })]);
            rates.Chart(new ChartBlock("Projected value: existing versus proposed", svg, data, "Values are shown in today's money at the intermediate growth rate."));
        }

        SectionBuilder charges = new("The effect of charges", startOnNewPage: true);
        charges.P("The table below follows COBS 13 Annex 4 2.2R: it shows what the proposed plan might be worth before charges, with plan and investment charges only, and after all charges including adviser charges.");
        charges.Table(new TableBlock(
            [new TableColumn("End of year", ColumnAlign.Right), new TableColumn("Payments in", ColumnAlign.Right), new TableColumn("Before charges", ColumnAlign.Right), new TableColumn("Plan and investment charges only", ColumnAlign.Right), new TableColumn("After all charges", ColumnAlign.Right), new TableColumn("Effect of deductions", ColumnAlign.Right)],
            [.. result.ReceivingRiy.EffectOfCharges.Select(e => new List<string> { Fmt.Count(e.Year), Fmt.Money(e.PaymentsToDate), Fmt.Money(e.BeforeCharges), Fmt.Money(e.PlanAndInvestmentChargesOnly), Fmt.Money(e.AfterAllCharges), Fmt.Money(e.EffectOfDeductionsToDate) })]));
        ChargeTotalsDto t = result.ReceivingRiy.TotalCharges;
        charges.Table(new TableBlock(
            [new TableColumn("Charge"), new TableColumn("Total over the term", ColumnAlign.Right)],
            [
                ["Platform", Fmt.Money(t.Platform)],
                ["Product", Fmt.Money(t.Product)],
                ["Funds", Fmt.Money(t.Fund)],
                ["Transaction costs", Fmt.Money(t.Transaction)],
                ["Adviser (initial)", Fmt.Money(t.AdviserInitial)],
                ["Adviser (ongoing)", Fmt.Money(t.AdviserOngoing)],
                ["Fixed fees", Fmt.Money(t.Fixed)],
                ["Dealing and switching", Fmt.Money(t.Dealing + t.Switch)],
                ["Discounts", Fmt.Money(t.Discounts)],
            ],
            Title: "Charges on the proposed plan",
            FooterRow: ["Total", Fmt.Money(t.Total)]));

        SectionBuilder holdings = new("Proposed investments");
        holdings.Table(new TableBlock(
            [new TableColumn("Fund"), new TableColumn("ISIN"), new TableColumn("Weight", ColumnAlign.Right), new TableColumn("Ongoing charge", ColumnAlign.Right)],
            [.. analysis.ProposedHoldings.Select(h => new List<string> { h.Name, h.Isin ?? "–", Fmt.Pct(h.WeightPct, 1), Fmt.Pct(h.OcfPct) })]));
        if (!string.IsNullOrWhiteSpace(analysis.Rationale))
        {
            holdings.H2("Adviser rationale").P(analysis.Rationale!);
        }

        return [summary.Build(), ClientSnapshot(r), Assumptions(r, [Fmt.Kv("Retirement age used", Fmt.Count(analysis.RetirementAge))]), verdicts.Build(), rates.Build(), charges.Build(), holdings.Build()];
    }

    private static List<string> RateRow(string label, CriticalYieldAtRateDto c) =>
        [$"{label} ({Fmt.Pct(c.GrowthPct, 1)})", Fmt.Money(c.ExistingValueAtRetirement), Fmt.Money(c.ReceivingValueAtRetirement), Fmt.Pct(c.CriticalYieldPct), Fmt.SignedPct(c.HeadroomPct)];

    private static string Verdict(Calculation.CriticalYield.SwitchVerdict v) => v switch
    {
        Calculation.CriticalYield.SwitchVerdict.SwitchCandidate => "Switch candidate",
        Calculation.CriticalYield.SwitchVerdict.Consider => "Consider",
        Calculation.CriticalYield.SwitchVerdict.Retain => "Retain",
        _ => "Refer",
    };

    // ------------------------------------------------------------------ DB transfer

    private static List<ReportSection> DbTransfer(ReportRequest r)
    {
        DbTransferAnalysisDto analysis = (DbTransferAnalysisDto)r.Analysis;
        DbTransferResultDto result = analysis.Result ?? throw new InvalidOperationException("The analysis has no result; calculate it before generating a report.");
        TvcDto tvc = result.Tvc;

        SectionBuilder comparator = new("Transfer Value Comparator");
        comparator.P("The Financial Conduct Authority requires this comparison in the prescribed format (COBS 19.1.3AR and COBS 19 Annex 5).");
        comparator.Quote(tvc.Wording);
        string svg = SvgCharts.Bars(new BarChart("How the transfer value compares", "Value today",
        [
            new BarItem("Transfer value offered", tvc.CashEquivalentTransferValue, ChartPalette.Proposed),
            new BarItem("Estimated current replacement cost of your pension income", tvc.EstimatedReplacementCost, ChartPalette.Existing),
        ]));
        comparator.Chart(new ChartBlock("Transfer Value Comparator", svg, new TableBlock(
            [new TableColumn("Measure"), new TableColumn("Value", ColumnAlign.Right)],
            [
                ["Transfer value offered", Fmt.Money(tvc.CashEquivalentTransferValue)],
                ["Estimated current replacement cost", Fmt.Money(tvc.EstimatedReplacementCost)],
                ["Difference", Fmt.Money(tvc.Difference)],
            ]), "The vertical axis starts at £0, as the rules require."));
        comparator.PageBreak();
        comparator.H2("Notes to the comparison");
        comparator.Numbered(tvc.Notes);
        comparator.Facts(
        [
            Fmt.Kv("Retirement age used", Fmt.Count(tvc.RetirementAgeUsed)),
            Fmt.Kv("Term to retirement", $"{Fmt.Num(tvc.TermYears, 1)} years"),
            Fmt.Kv("Gilt yield used", Fmt.Pct(tvc.GiltYieldUsedPct)),
            Fmt.Kv("Discount rate after the 0.4% product charge", Fmt.Pct(tvc.DiscountRateUsedPct)),
            Fmt.Kv("Scheme pension at retirement", $"{Fmt.Money(tvc.PensionAtRetirement)} a year"),
            Fmt.Kv("Cost of buying that income at retirement", Fmt.Money(tvc.AnnuityCostAtRetirement)),
        ], "Basis of the comparison");

        SectionBuilder benefits = new("Your scheme benefits", startOnNewPage: true);
        benefits.P("Each tranche of your pension is revalued to retirement on the basis the Handbook prescribes (COBS 19 Annex 4C 1R(4)) and priced as an annuity on the prescribed basis.");
        benefits.Table(new TableBlock(
            [new TableColumn("Tranche"), new TableColumn("Accrued at leaving", ColumnAlign.Right), new TableColumn("Revaluation", ColumnAlign.Right), new TableColumn("Years", ColumnAlign.Right), new TableColumn("Pension at retirement", ColumnAlign.Right), new TableColumn("Increases in payment", ColumnAlign.Right), new TableColumn("Annuity rate", ColumnAlign.Right), new TableColumn("Cost", ColumnAlign.Right)],
            [.. tvc.Tranches.Select(x => new List<string> { x.Name + (x.IsGmp ? " (GMP)" : string.Empty), Fmt.Money(x.AccruedAnnualPension), Fmt.Pct(x.RevaluationRatePct), Fmt.Count(x.YearsRevalued), Fmt.Money(x.PensionAtRetirement), Fmt.Pct(x.EscalationInPaymentPct), Fmt.Pct(x.AnnuityInterestRatePct), Fmt.Money(x.AnnuityCost) })],
            FooterRow: ["Total", string.Empty, string.Empty, string.Empty, Fmt.Money(tvc.PensionAtRetirement), string.Empty, string.Empty, Fmt.Money(tvc.AnnuityCostAtRetirement)]));

        SectionBuilder yields = new("Critical yields");
        yields.P("The FCA replaced the critical yield with the Transfer Value Comparator in 2018 (PS18/6). These figures are shown for information only and are not an FCA requirement.", ParagraphStyle.Quote);
        yields.Facts(
        [
            Fmt.Kv("To match the scheme pension by buying an annuity", Fmt.Pct(result.CriticalYields.TypeAAnnuityMatchPct)),
            Fmt.Kv("To match tax-free cash plus the reduced pension", Fmt.Pct(result.CriticalYields.TypeBPclsAndReducedPensionPct)),
            Fmt.Kv("To sustain the scheme pension as drawdown to age " + Fmt.Count(analysis.PlanEndAge), Fmt.Pct(result.CriticalYields.DrawdownHurdleRatePct)),
            Fmt.Kv("Scheme tax-free cash (at the scheme's commutation factor)", Fmt.Money(result.CriticalYields.SchemePcls)),
            Fmt.Kv("Residual pension after commutation", $"{Fmt.Money(result.CriticalYields.ResidualPensionAfterPcls)} a year"),
        ]);

        SectionBuilder income = new("Income comparison", startOnNewPage: true);
        income.P($"The scheme pension is compared with a sustainable income from the transferred fund, in today's money, assuming growth of {Fmt.Pct(analysis.AptaGrowthPct ?? 0m)} a year before charges and a plan running to age {Fmt.Count(analysis.PlanEndAge)} (COBS 19 Annex 4A).");
        income.Table(new TableBlock(
            [new TableColumn("Age", ColumnAlign.Right), new TableColumn("Scheme pension (then)", ColumnAlign.Right), new TableColumn("Scheme pension (today's money)", ColumnAlign.Right), new TableColumn("Drawdown income (today's money)", ColumnAlign.Right), new TableColumn("Remaining fund", ColumnAlign.Right), new TableColumn("Death benefit", ColumnAlign.Right)],
            [.. result.IncomeComparison.Select(i => new List<string> { Fmt.Count(i.Age), Fmt.Money(i.SchemeIncomeNominal), Fmt.Money(i.SchemeIncomeReal), Fmt.Money(i.DrawdownIncomeReal), Fmt.Money(i.ResidualFundReal), Fmt.Money(i.SchemeDeathBenefitReal) })]));
        if (result.IncomeComparison.Count > 1)
        {
            string incomeSvg = SvgCharts.Line(new LineChart("Income in today's money", "Age", "Annual income",
            [
                new LineSeries("Scheme pension", [.. result.IncomeComparison.Select(i => new ChartPoint(i.Age, i.SchemeIncomeReal))], ChartPalette.Existing, Dashed: true),
                new LineSeries("Drawdown from the transfer", [.. result.IncomeComparison.Select(i => new ChartPoint(i.Age, i.DrawdownIncomeReal))], ChartPalette.Proposed),
            ]));
            income.Chart(new ChartBlock("Scheme pension versus drawdown", incomeSvg, new TableBlock(
                [new TableColumn("Age", ColumnAlign.Right), new TableColumn("Scheme", ColumnAlign.Right), new TableColumn("Drawdown", ColumnAlign.Right)],
                [.. result.IncomeComparison.Select(i => new List<string> { Fmt.Count(i.Age), Fmt.Money(i.SchemeIncomeReal), Fmt.Money(i.DrawdownIncomeReal) })])));
        }

        income.H2("If things do not go to plan");
        income.Table(new TableBlock(
            [new TableColumn("Scenario"), new TableColumn("Sustainable income (today's money)", ColumnAlign.Right), new TableColumn("Change", ColumnAlign.Right)],
            [.. result.StressTests.Select(s => new List<string> { s.Name, Fmt.Money(s.SustainableRealIncome), Fmt.Money(s.Change) })],
            Note: "Stress tests are required by COBS 19 Annex 4A 5R."));

        OnePageSummaryDto summaryDto = result.Summary;
        SectionBuilder onePage = new("Summary of advice", startOnNewPage: true);
        onePage.P("This page summarises the charges and the cost of advice as COBS 9.4.11R requires.");
        onePage.Placeholder("Recommendation: [the adviser records the recommendation to transfer or to remain, and the client signs to confirm they have read it].");
        onePage.Facts(
        [
            Fmt.Kv("Cost of initial advice", Fmt.Money(summaryDto.InitialAdviceFee)),
            Fmt.Kv("Revalued monthly income given up", Fmt.Money2(summaryDto.RevaluedMonthlyIncome)),
            Fmt.Kv("Months of that income needed to pay for the advice", Fmt.Count(summaryDto.PaybackMonths)),
            Fmt.Kv("Charges in the first year (proposed plan)", Fmt.Money(summaryDto.FirstYearChargesProposed)),
            Fmt.Kv("Ongoing charges each year (proposed plan)", Fmt.Money(summaryDto.OngoingAnnualChargesProposed)),
            Fmt.Kv("Charges in the ceding arrangement", Fmt.Money(summaryDto.FirstYearChargesCeding)),
            Fmt.Kv("Charges in the workplace default", Fmt.Money(summaryDto.FirstYearChargesWorkplaceDefault)),
            Fmt.Kv("Adviser charging basis", analysis.ChargeBasis == AdviserChargeBasis.Contingent ? $"Contingent — {Fmt.OrDash(analysis.ContingentChargingCarveOut)}" : "Not contingent on the outcome"),
        ]);
        if (analysis.ChargeBasis == AdviserChargeBasis.Contingent)
        {
            onePage.Callout(CalloutKind.Warning, "Contingent charging", ["Contingent charging for defined benefit transfer advice is banned except in the narrow cases in COBS 19.1B.9R. The carve-out relied on is recorded above and evidence must be retained."]);
        }

        if (result.Warnings.Count > 0)
        {
            onePage.Callout(CalloutKind.Warning, "Points requiring adviser judgement", result.Warnings);
        }

        onePage.P($"Life expectancy at retirement on the mortality basis used is {Fmt.Num(result.LifeExpectancyAtRetirement, 1)} years. The plan models income beyond average life expectancy, to age {Fmt.Count(analysis.PlanEndAge)}.", ParagraphStyle.Quote);

        return [comparator.Build(), ClientSnapshot(r), Assumptions(r), benefits.Build(), yields.Build(), income.Build(), onePage.Build()];
    }

    // ------------------------------------------------------------------ cashflow

    private static List<ReportSection> Cashflow(ReportRequest r)
    {
        CashflowPlanDto plan = (CashflowPlanDto)r.Analysis;
        CashflowResultDto result = plan.Result ?? throw new InvalidOperationException("The plan has no result; calculate it before generating a report.");

        SectionBuilder summary = new("Summary");
        summary.Facts(
        [
            Fmt.Kv("Plan", plan.Title),
            Fmt.Kv("Runs to age", Fmt.Count(plan.PlanEndAge)),
            Fmt.Kv("Outcome", result.Succeeds ? "No shortfall on the assumptions used" : $"First shortfall at age {Fmt.Count(result.FirstShortfallAge ?? 0)}"),
            Fmt.Kv("Sustainable level spending (today's money)", Fmt.Money(result.SustainableSpend)),
            Fmt.Kv("Estate at the end of the plan (today's money)", Fmt.Money(result.LegacyAtEndReal)),
            Fmt.Kv("Total income tax over the plan", Fmt.Money(result.TotalIncomeTax)),
            Fmt.Kv("Tax-free cash allowance used", Fmt.Money(result.LumpSumAllowanceUsed)),
        ]);
        if (!result.Succeeds)
        {
            summary.Callout(CalloutKind.Warning, "The plan runs out of money", [$"On these assumptions the plan cannot meet spending from age {Fmt.Count(result.FirstShortfallAge ?? 0)}. The total shortfall over the plan is {Fmt.Money(result.TotalShortfall)}."]);
        }

        IReadOnlyList<CashflowRowDto> rows = result.Rows;
        SectionBuilder table = new("Year by year", startOnNewPage: true);
        table.P("Amounts are as they arise (not adjusted for inflation) except the final column, which is in today's money.");
        table.Table(new TableBlock(
            [new TableColumn("Age", ColumnAlign.Right), new TableColumn("Earnings", ColumnAlign.Right), new TableColumn("State Pension", ColumnAlign.Right), new TableColumn("Other pensions", ColumnAlign.Right), new TableColumn("Withdrawals", ColumnAlign.Right), new TableColumn("Tax", ColumnAlign.Right), new TableColumn("Net income", ColumnAlign.Right), new TableColumn("Spending", ColumnAlign.Right), new TableColumn("Shortfall", ColumnAlign.Right), new TableColumn("Assets (today's money)", ColumnAlign.Right)],
            [.. rows.Select(x => new List<string>
            {
                Fmt.Count(x.Age), Fmt.Money(x.EmploymentIncome), Fmt.Money(x.StatePensionIncome), Fmt.Money(x.DbPensionIncome),
                Fmt.Money(x.PensionWithdrawalsTaxable + x.TaxFreeCash + x.IsaWithdrawals + x.GiaWithdrawals + x.CashWithdrawals),
                Fmt.Money(x.IncomeTax + x.NationalInsurance + x.CapitalGainsTax), Fmt.Money(x.NetIncome), Fmt.Money(x.Expenses),
                x.Shortfall > 0m ? Fmt.Money(x.Shortfall) : "–", Fmt.Money(x.TotalAssetsReal),
            })]));

        SectionBuilder charts = new("How your assets change", startOnNewPage: true);
        if (rows.Count > 1)
        {
            List<string> kinds = [.. rows.SelectMany(x => x.Assets.Select(a => Fmt.Words(a.Kind))).Distinct()];
            List<AreaSeries> series = [.. kinds.Select((k, i) => new AreaSeries(k, [.. rows.Select(x => x.Assets.Where(a => Fmt.Words(a.Kind) == k).Sum(a => a.ValueReal))], ChartPalette.Series[i % ChartPalette.Series.Count]))];
            string svg = SvgCharts.StackedArea(new StackedAreaChart("Assets in today's money", "Age", "Value", [.. rows.Select(x => (decimal)x.Age)], series));
            charts.Chart(new ChartBlock("Assets by type", svg, new TableBlock(
                [new TableColumn("Age", ColumnAlign.Right), .. kinds.Select(k => new TableColumn(k, ColumnAlign.Right))],
                [.. rows.Where((_, i) => i % Math.Max(1, rows.Count / 12) == 0 || i == rows.Count - 1).Select(x =>
                {
                    List<string> cells = [Fmt.Count(x.Age)];
                    cells.AddRange(kinds.Select(k => Fmt.Money(x.Assets.Where(a => Fmt.Words(a.Kind) == k).Sum(a => a.ValueReal))));
                    return (IReadOnlyList<string>)cells;
                })])));
        }

        if (plan.StochasticResult is { } stochastic)
        {
            SectionBuilder mc = new("If markets do not behave as assumed", startOnNewPage: true);
            mc.P($"The plan was run {Fmt.Count(stochastic.Paths)} times with randomly varying investment returns (seed {stochastic.Seed}, so the run can be reproduced exactly).");
            mc.Facts(
            [
                Fmt.Kv("Plans with no shortfall", Fmt.Pct(stochastic.ProbabilityOfSuccess * 100m, 1)),
                Fmt.Kv("Median shortfall age", stochastic.MedianShortfallAge is { } m ? Fmt.Count(m) : "no shortfall in the median case"),
                Fmt.Kv("Worst tenth of outcomes: shortfall from age", stochastic.WorstDecileShortfallAge is { } w ? Fmt.Count(w) : "no shortfall"),
                Fmt.Kv("Average estate at the end (today's money)", Fmt.Money(stochastic.MeanLegacyReal)),
                Fmt.Kv("Median no less conservative than the deterministic plan (COBS 19.1.2CR)", Fmt.YesNo(stochastic.Conservativeness.MedianIsNoLessConservative)),
            ]);
            string fan = SvgCharts.Fan(new FanChart("Range of outcomes", "Age", "Assets (today's money)",
                [.. stochastic.TotalAssetsReal.Select(p => new FanPoint(p.Age, p.P10, p.P25, p.P50, p.P75, p.P90))]));
            mc.Chart(new ChartBlock("Range of outcomes", fan, new TableBlock(
                [new TableColumn("Age", ColumnAlign.Right), new TableColumn("Worst tenth", ColumnAlign.Right), new TableColumn("Lower quarter", ColumnAlign.Right), new TableColumn("Median", ColumnAlign.Right), new TableColumn("Upper quarter", ColumnAlign.Right), new TableColumn("Best tenth", ColumnAlign.Right)],
                [.. stochastic.TotalAssetsReal.Where((_, i) => i % Math.Max(1, stochastic.TotalAssetsReal.Count / 10) == 0 || i == stochastic.TotalAssetsReal.Count - 1).Select(p => new List<string> { Fmt.Count(p.Age), Fmt.Money(p.P10), Fmt.Money(p.P25), Fmt.Money(p.P50), Fmt.Money(p.P75), Fmt.Money(p.P90) })])));
            return [summary.Build(), ClientSnapshot(r), Assumptions(r), table.Build(), charts.Build(), mc.Build()];
        }

        return [summary.Build(), ClientSnapshot(r), Assumptions(r), table.Build(), charts.Build()];
    }

    // ------------------------------------------------------------------ fund comparison

    private static List<ReportSection> FundComparison(ReportRequest r)
    {
        IReadOnlyList<HoldingDto> proposed = r.Analysis switch
        {
            PensionSwitchAnalysisDto p => p.ProposedHoldings,
            DbTransferAnalysisDto d => d.ProposedHoldings,
            _ => [],
        };
        decimal weighted = proposed.Count == 0 ? 0m : proposed.Sum(h => h.WeightPct * (h.OcfPct ?? 0m)) / Math.Max(1m, proposed.Sum(h => h.WeightPct));

        SectionBuilder s = new("Proposed investments");
        s.Table(new TableBlock(
            [new TableColumn("Fund"), new TableColumn("ISIN"), new TableColumn("Weight", ColumnAlign.Right), new TableColumn("Ongoing charge", ColumnAlign.Right)],
            [.. proposed.Select(h => new List<string> { h.Name, h.Isin ?? "–", Fmt.Pct(h.WeightPct, 1), Fmt.Pct(h.OcfPct) })],
            FooterRow: ["Weighted average", string.Empty, Fmt.Pct(proposed.Sum(h => h.WeightPct), 1), Fmt.Pct(weighted)],
            Note: "Ongoing charges figures are those recorded in the fund catalogue at the date of this report."));
        return [s.Build(), Assumptions(r)];
    }

    // ------------------------------------------------------------------ suitability

    private static List<ReportSection> Suitability(ReportRequest r)
    {
        SectionBuilder intro = new("Demands and needs");
        intro.P("This report sets out the advice given, why it is considered suitable for you, and the disadvantages you should weigh before deciding (COBS 9.4.7R).");
        intro.Placeholder("[Record the client's objectives, timescales, knowledge and experience, attitude to risk and capacity for loss.]");

        SectionBuilder recommendation = new("Recommendation");
        recommendation.Placeholder("[Record the recommendation and the reasons it meets the demands and needs above.]");

        List<ReportSection> analysisSections = r.Analysis switch
        {
            PensionSwitchAnalysisDto => PensionSwitch(r with { Kind = ReportKind.PensionSwitch }),
            DbTransferAnalysisDto => DbTransfer(r with { Kind = ReportKind.DbTransfer }),
            CashflowPlanDto => Cashflow(r with { Kind = ReportKind.Cashflow }),
            _ => [],
        };

        ReportSection analysisSection = new SectionBuilder("Why this is suitable")
            .Placeholder("[Explain how the analysis that follows supports the recommendation, including the charges the client will pay and the features they gain or give up.]")
            .Build();

        SectionBuilder disadvantages = new("Disadvantages and risks");
        disadvantages.Bullets(
        [
            "Investment returns are not guaranteed; the value of your plan can fall as well as rise.",
            "Charges reduce the value of your plan; the effect over the full term is shown in the analysis.",
            "Guarantees, protected tax-free cash or a protected pension age in an existing plan are lost on transfer.",
            "Tax rules and allowances can change, and the value of any tax treatment depends on your circumstances.",
        ]);
        disadvantages.Placeholder("[Add any risks specific to this recommendation.]");

        SectionBuilder ongoing = new("Ongoing service");
        ongoing.Placeholder("[State the ongoing service to be provided, its cost in cash terms, when it will be reviewed and how it can be cancelled.]");

        SectionBuilder declaration = new("Declaration");
        declaration.P($"Prepared by {r.GeneratedBy} of {r.FirmName} (FRN {r.FirmReferenceNumber}) on {Fmt.Stamp(r.GeneratedAtUtc)}.");
        declaration.Placeholder("[Adviser signature and client acknowledgement.]");

        List<ReportSection> sections = [intro.Build(), ClientSnapshot(r), recommendation.Build(), analysisSection, disadvantages.Build(), ongoing.Build()];
        sections.AddRange(analysisSections.Where(section => section.Title != "Your circumstances"));
        sections.Add(declaration.Build());
        return sections;
    }
}
