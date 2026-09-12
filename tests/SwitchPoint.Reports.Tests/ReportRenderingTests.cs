using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Ports;
using SwitchPoint.Calculation.CriticalYield;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Reports.Charts;
using SwitchPoint.Reports.Composition;
using SwitchPoint.Reports.Model;
using SwitchPoint.Reports.Rendering;
using Xunit;

namespace SwitchPoint.Reports.Tests;

/// <summary>Plausible DTOs standing in for a calculated analysis.</summary>
public static class Fixtures
{
    public static readonly DateTime GeneratedAt = new(2026, 9, 7, 9, 30, 0, DateTimeKind.Utc);
    public static readonly Guid AnalysisId = new("7f3c2b10-9b1e-4a55-9d2e-2f0b7a1c4e88");
    public const string ResultHash = "9f2c1a77b3e4d5061f2a3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d6e7f80";

    public static ClientDetail Client() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Ms",
        FirstName = "Sarah",
        LastName = "Mitchell",
        FullName = "Ms Sarah Mitchell",
        DateOfBirth = new DateOnly(1971, 3, 14),
        Age = 55,
        Sex = Sex.Female,
        Email = "sarah.mitchell@example.com",
        MaritalStatus = MaritalStatus.Married,
        EmploymentStatus = EmploymentStatus.Employed,
        AnnualSalary = 58_000m,
        TargetRetirementAge = 67,
        TaxRegime = TaxRegime.RestOfUk,
        RiskProfile = 5,
        Health = HealthStatus.Standard,
        StatePension = new StatePensionDto(241.30m, 34),
        NationalInsuranceNumberMasked = "******56A",
        ExternalReference = new ExternalReferenceDto(ExternalSource.Manual, null),
        Schemes = [],
        CreatedAtUtc = GeneratedAt,
        UpdatedAtUtc = GeneratedAt,
    };

    public static AssumptionSetDto Assumptions() => new()
    {
        Id = Guid.NewGuid(),
        Name = "FCA standard 2026/27",
        IsFcaStandard = true,
        Version = 1,
        GrowthLowerPct = 2m,
        GrowthIntermediatePct = 5m,
        GrowthHigherPct = 8m,
        InflationPct = 2m,
        EarningsGrowthPct = 3.5m,
        RpiInflationPct = 3m,
        ChargeInflationPct = 2m,
        PreRetirementProductChargePct = 0.4m,
        AnnuityExpenseLoadingPct = 4m,
        SpouseAgeGapYears = 3,
        MortalityBasis = MortalityBasis.OnsNationalLifeTables2020_22,
        StatePensionIncreasePct = 3.5m,
        TaxYear = "2026/27",
        ProjectionBasis = ProjectionBasis.Real,
        MarketInputs = new MarketInputsDto(4.2m, 4.4m, 4.6m, 4.7m, 0.8m, 4m, 1m, new DateOnly(2026, 8, 15)),
    };

    private static RiyDto Riy(decimal product, decimal total) => new(
        5m, product, total, 5m - product, 5m - total, 250_000m, 238_000m, 232_000m,
        $"Product charges reduce investment growth after price inflation from 5.0% to {5m - product:0.0}%.",
        $"All charges reduce investment growth after price inflation from 5.0% to {5m - total:0.0}%.",
        [
            new EffectOfChargesRowDto(1, 2_400m, 105_000m, 104_600m, 104_100m, 900m),
            new EffectOfChargesRowDto(3, 7_200m, 118_000m, 116_800m, 115_600m, 2_400m),
            new EffectOfChargesRowDto(5, 12_000m, 132_000m, 129_700m, 127_500m, 4_500m),
            new EffectOfChargesRowDto(12, 28_800m, 205_000m, 196_000m, 189_000m, 16_000m),
        ],
        new ChargeTotalsDto(3_100m, 0m, 2_700m, 600m, 2_345m, 8_900m, 0m, 180m, 0m, -120m, 0m, 0m, 17_705m));

    public static PensionSwitchAnalysisDto PensionSwitch()
    {
        PensionSwitchResultDto result = new(
            234_500m, 2_345m, true,
            ["'Legacy Personal Pension (with-profits)' has guarantees or protected features (FSA switching outcome 2); it is referred for adviser judgement."],
            new CriticalYieldAtRateDto(2m, 268_000m, 271_000m, 1.678m, -0.316m, 0.322m, 3_000m, 4, true),
            new CriticalYieldAtRateDto(5m, 372_000m, 379_500m, 4.669m, 2.617m, 0.331m, 7_500m, 3, true),
            new CriticalYieldAtRateDto(8m, 516_000m, 528_000m, 7.66m, 5.549m, 0.34m, 12_000m, 3, true),
            Riy(0.52m, 1.013m),
            [
                new CedingSchemeResultDto("Employer Group Personal Pension", 96_500m, 96_500m, 158_000m, Riy(0.45m, 0.701m), Riy(0.52m, 1.018m), 5.18m, false, SwitchVerdict.Retain),
                new CedingSchemeResultDto("Legacy Personal Pension (with-profits)", 142_000m, 138_000m, 214_000m, Riy(2.05m, 2.229m), Riy(0.52m, 1.041m), 3.44m, true, SwitchVerdict.Refer),
            ],
            [.. Enumerable.Range(0, 13).Select(y => new ChartPointDto(y, 238_500m * (decimal)Math.Pow(1.03, y), 234_500m * (decimal)Math.Pow(1.035, y)))],
            "1.0.0", GeneratedAt, new AssumptionSetRefDto(Guid.NewGuid(), "FCA standard 2026/27", 1));

        return new PensionSwitchAnalysisDto
        {
            Id = AnalysisId,
            FirmId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            Title = "Consolidate two plans",
            RetirementAge = 67,
            CedingSchemeIds = [Guid.NewGuid(), Guid.NewGuid()],
            ProposedProductId = Guid.NewGuid(),
            ProposedProductChargeVersion = 1,
            ProposedHoldings = [new HoldingDto("Vanguard LifeStrategy 60% Equity", 100m, "GB00B3TYHH97", null, 0.22m)],
            ProposedAdviserCharges = new AdviserChargeDto(1m, 0m, 0.5m, 0m),
            AssumptionSetId = Guid.NewGuid(),
            Rationale = "Consolidation reduces ongoing cost and gives access to drawdown, which the ceding plans do not offer.",
            Status = AnalysisStatus.Calculated,
            Version = 1,
            ResultHash = ResultHash,
            CalculatedAtUtc = GeneratedAt,
            EngineVersion = "1.0.0",
            Result = result,
            CreatedAtUtc = GeneratedAt,
            UpdatedAtUtc = GeneratedAt,
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static DbTransferAnalysisDto DbTransfer()
    {
        DbTransferResultDto result = new(
            new TvcDto(486_000m, 426_902m, -59_098m, 65, 9.77m, 4.4m, 4m, 630_000m, 30_973m,
                "You have been offered a cash equivalent transfer value of £486,000 in exchange for you giving up any future claims to a pension from the scheme. Will I be better or worse off by transferring? It could cost you £426,902 to obtain a comparable level of income from an insurer. This means the same retirement income could cost you £59,098 less by transferring.",
                [
                    "The estimated replacement cost is based on the income the scheme would pay at its normal retirement age, including a spouse's pension, for an average healthy person, using today's costs.",
                    "The estimated replacement value takes into account investment returns after product charges that you might obtain from risk-free investments.",
                    "No allowance has been made for taxation or adviser charges prior to benefits commencing.",
                ],
                [
                    // A GMP does not escalate in payment: pricing and nominal escalation are both zero.
                    new RevaluedTrancheDto("Pre-97 GMP", 1_850m, 4.75m, 20, 4_670m, 0m, 0m, 4m, 16.2m, 75_654m, true),

                    // An LPI(CPI) tranche capped at 2.5% is priced at the level rate with a 2.5% escalation,
                    // so here the pricing and nominal rates coincide.
                    new RevaluedTrancheDto("Post-2005", 6_100m, 2m, 20, 9_064m, 2.5m, 2.5m, 4m, 21.4m, 193_970m, false),

                    // An index-linked tranche is the case the disclosure column exists for: it is priced with
                    // a zero escalation inside a reduced real interest rate, but the pension really does rise
                    // at 2%, and that is the figure the report must show.
                    new RevaluedTrancheDto("1997–2005", 9_400m, 2m, 20, 13_968m, 0m, 2m, 3m, 17.2m, 240_250m, false),
                ]),
            new CriticalYieldsDto(3.73m, 3.6m, 5.24m, 118_400m, 24_395m, true),
            20_842m,
            [
                new IncomeComparisonRowDto(65, 30_973m, 25_400m, 20_842m, 486_000m, 210_000m),
                new IncomeComparisonRowDto(70, 33_500m, 24_900m, 20_842m, 402_000m, 190_000m),
                new IncomeComparisonRowDto(75, 36_200m, 24_300m, 20_842m, 305_000m, 165_000m),
                new IncomeComparisonRowDto(80, 39_100m, 23_800m, 20_842m, 190_000m, 138_000m),
                new IncomeComparisonRowDto(85, 42_300m, 23_300m, 20_842m, 58_000m, 110_000m),
            ],
            [
                new StressScenarioDto("Base case", 20_842m, 0m),
                new StressScenarioDto("Growth 2% lower", 17_100m, -3_742m),
                new StressScenarioDto("Inflation 1% higher", 19_050m, -1_792m),
                new StressScenarioDto("Fund falls 20% at retirement", 16_674m, -4_168m),
                new StressScenarioDto("Lives to 105", 18_900m, -1_942m),
            ],
            new OnePageSummaryDto(9_000m, 1_863m, 5, 15_200m, 5_490m, 0m, 3_645m),
            19.4m,
            [],
            "1.0.0", GeneratedAt, new AssumptionSetRefDto(Guid.NewGuid(), "FCA standard 2026/27", 1));

        return new DbTransferAnalysisDto
        {
            Id = AnalysisId,
            FirmId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            DbSchemeId = Guid.NewGuid(),
            TransferDate = new DateOnly(2026, 10, 1),
            PlanEndAge = 100,
            ProposedProductId = Guid.NewGuid(),
            ProposedHoldings = [new HoldingDto("Vanguard LifeStrategy 60% Equity", 100m, "GB00B3TYHH97", null, 0.22m)],
            ProposedAdviserCharges = new AdviserChargeDto(2m, 0m, 0.75m, 0m),
            AptaGrowthPct = 5m,
            ChargeBasis = AdviserChargeBasis.NonContingent,
            InitialAdviceFee = 9_000m,
            WorkplaceDefaultChargePct = 0.75m,
            AssumptionSetId = Guid.NewGuid(),
            Status = AnalysisStatus.Calculated,
            Version = 1,
            ResultHash = ResultHash,
            CalculatedAtUtc = GeneratedAt,
            EngineVersion = "1.0.0",
            Result = result,
            CreatedAtUtc = GeneratedAt,
            UpdatedAtUtc = GeneratedAt,
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static CashflowPlanDto Cashflow(bool withStochastic)
    {
        List<CashflowRowDto> rows = [.. Enumerable.Range(0, 40).Select(i =>
        {
            int age = 55 + i;
            decimal assets = 600_000m * (decimal)Math.Pow(1.02, i) - (i > 10 ? 25_000m * (i - 10) : 0m);
            return new CashflowRowDto(i + 1, age, null,
                age < 67 ? 58_000m : 0m, age >= 67 ? 12_548m : 0m, 0m, 0m,
                age >= 67 ? 22_000m : 0m, age >= 67 ? 7_300m : 0m, 0m, 0m, 0m,
                age >= 67 ? 3_100m : 9_432m, age < 67 ? 3_954m : 0m, 0m,
                age < 67 ? 44_614m : 38_748m, age < 67 ? 44_614m : 30_000m,
                38_000m, 0m, age > 78 && i % 7 == 0 ? 1_200m : 0m, age < 67 ? 10_000m : 0m,
                [new AssetValueDto("SIPP", PlanAssetKind.UncrystallisedPension, assets * 0.7m, assets * 0.65m), new AssetValueDto("ISA", PlanAssetKind.Isa, assets * 0.3m, assets * 0.28m)],
                assets, assets * 0.93m, []);
        })];

        CashflowResultDto result = new(rows, 89, 148_000m, 4_800m, 92_000m, 61_000m, 134_000m, false, 43_610m, "1.0.0", GeneratedAt, new AssumptionSetRefDto(Guid.NewGuid(), "FCA standard 2026/27", 1));

        StochasticResultDto? stochastic = withStochastic
            ? new StochasticResultDto("7", 1000, 0.72m,
                [.. rows.Select(r => new PercentileRowDto(r.Year, r.Age, r.TotalAssetsReal * 0.55m, r.TotalAssetsReal * 0.68m, r.TotalAssetsReal * 0.84m, r.TotalAssetsReal, r.TotalAssetsReal * 1.18m, r.TotalAssetsReal * 1.36m, r.TotalAssetsReal * 1.52m))],
                [.. rows.Select(r => new PercentileRowDto(r.Year, r.Age, r.NetIncomeReal * 0.6m, r.NetIncomeReal * 0.75m, r.NetIncomeReal * 0.9m, r.NetIncomeReal, r.NetIncomeReal * 1.1m, r.NetIncomeReal * 1.2m, r.NetIncomeReal * 1.3m))],
                91, 84, new ConservativenessDto(61_000m, 58_400m, true), 74_000m, "1.0.0", GeneratedAt)
            : null;

        return new CashflowPlanDto
        {
            Id = AnalysisId,
            FirmId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            Title = "Retirement plan",
            PlanEndAge = 95,
            AssumptionSetId = Guid.NewGuid(),
            Status = AnalysisStatus.Calculated,
            Version = 1,
            ResultHash = ResultHash,
            CalculatedAtUtc = GeneratedAt,
            EngineVersion = "1.0.0",
            Result = result,
            StochasticResult = stochastic,
            CreatedAtUtc = GeneratedAt,
            UpdatedAtUtc = GeneratedAt,
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static ReportRequest Request(ReportKind kind, ReportFormat format, object analysis) =>
        new(kind, format, AnalysisId, 1, ResultHash, analysis, Client(), "Demo Financial Planning Ltd", "000000", Assumptions(), "Alex Adviser", GeneratedAt);
}

public class PdfRenderingTests
{
    private static readonly ReportRenderer Renderer = new ReportRenderer();

    private static string SamplesDirectory()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "samples");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static object AnalysisFor(ReportKind kind) => kind switch
    {
        ReportKind.DbTransfer => Fixtures.DbTransfer(),
        ReportKind.Cashflow => Fixtures.Cashflow(withStochastic: true),
        _ => Fixtures.PensionSwitch(),
    };

    [Theory]
    [InlineData(ReportKind.PensionSwitch)]
    [InlineData(ReportKind.DbTransfer)]
    [InlineData(ReportKind.Cashflow)]
    [InlineData(ReportKind.FundComparison)]
    [InlineData(ReportKind.Suitability)]
    public async Task Every_kind_renders_a_pdf(ReportKind kind)
    {
        object analysis = AnalysisFor(kind);
        ReportDocument doc = await Renderer.RenderAsync(Fixtures.Request(kind, ReportFormat.Pdf, analysis));
        Assert.Equal("application/pdf", doc.ContentType);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(doc.Content.Span[..4]), StringComparison.Ordinal);
        Assert.True(doc.Content.Length > 10_000, $"{kind} PDF was only {doc.Content.Length} bytes");
        Assert.EndsWith(".pdf", doc.FileName, StringComparison.Ordinal);
        Assert.Equal(ReportRenderer.TemplateVersion, doc.TemplateVersion);
        await File.WriteAllBytesAsync(Path.Combine(SamplesDirectory(), $"{kind}.pdf"), doc.Content.ToArray());
    }

    [Fact]
    public async Task Cashflow_without_a_stochastic_run_still_renders()
    {
        ReportDocument doc = await Renderer.RenderAsync(Fixtures.Request(ReportKind.Cashflow, ReportFormat.Pdf, Fixtures.Cashflow(withStochastic: false)));
        Assert.True(doc.Content.Length > 10_000);
    }

    [Fact]
    public async Task An_uncalculated_analysis_is_rejected()
    {
        PensionSwitchAnalysisDto analysis = Fixtures.PensionSwitch() with { Result = null };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Renderer.RenderAsync(Fixtures.Request(ReportKind.PensionSwitch, ReportFormat.Pdf, analysis)));
    }
}

public class DocxRenderingTests
{
    private static readonly ReportRenderer Renderer = new ReportRenderer();

    [Theory]
    [InlineData(ReportKind.PensionSwitch)]
    [InlineData(ReportKind.DbTransfer)]
    [InlineData(ReportKind.Cashflow)]
    [InlineData(ReportKind.Suitability)]
    public async Task Every_kind_renders_a_readable_docx(ReportKind kind)
    {
        object analysis = kind == ReportKind.Cashflow ? Fixtures.Cashflow(withStochastic: true) : kind == ReportKind.DbTransfer ? Fixtures.DbTransfer() : Fixtures.PensionSwitch();
        ReportDocument doc = await Renderer.RenderAsync(Fixtures.Request(kind, ReportFormat.Docx, analysis));
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", doc.ContentType);
        using MemoryStream stream = new(doc.Content.ToArray());
        using WordprocessingDocument word = WordprocessingDocument.Open(stream, false);
        MainDocumentPart main = word.MainDocumentPart ?? throw new InvalidOperationException("no main part");
        Body body = main.Document?.Body ?? throw new InvalidOperationException("no body");
        string text = body.InnerText;
        Assert.Contains("Audit statement", text, StringComparison.Ordinal);
        Assert.Contains(Fixtures.ResultHash, text, StringComparison.Ordinal);
        Assert.Contains("Demo Financial Planning Ltd", text, StringComparison.Ordinal);
        Assert.True(body.Descendants<Table>().Any());
        if (kind == ReportKind.DbTransfer)
        {
            Assert.Contains("cash equivalent transfer value", text, StringComparison.Ordinal);
            Assert.Contains("not an FCA requirement", text, StringComparison.Ordinal);
        }
    }
}

public class JsonRenderingTests
{
    private static readonly ReportRenderer Renderer = new ReportRenderer();

    [Fact]
    public async Task Json_is_deterministic_and_carries_the_result_hash()
    {
        ReportRequest request = Fixtures.Request(ReportKind.PensionSwitch, ReportFormat.Json, Fixtures.PensionSwitch());
        ReportDocument a = await Renderer.RenderAsync(request);
        ReportDocument b = await Renderer.RenderAsync(request);
        Assert.Equal(a.Content.ToArray(), b.Content.ToArray());
        string json = Encoding.UTF8.GetString(a.Content.Span);
        Assert.Contains(Fixtures.ResultHash, json, StringComparison.Ordinal);
        Assert.Contains("\"criticalYieldPct\":4.669", json, StringComparison.Ordinal);
        Assert.Equal("application/json", a.ContentType);
    }
}

public class ComposerTests
{
    [Fact]
    public void Pension_switch_model_has_the_expected_sections()
    {
        ReportModel model = ReportComposer.Compose(Fixtures.Request(ReportKind.PensionSwitch, ReportFormat.Pdf, Fixtures.PensionSwitch()), "test");
        List<string> titles = [.. model.Sections.Select(s => s.Title)];
        Assert.Equal(["Executive summary", "Your circumstances", "Assumptions used", "Each existing arrangement", "Critical yield at the standardised rates", "The effect of charges", "Proposed investments", "Audit statement"], titles);
        ReportSection summary = model.Sections[0];
        Assert.Contains(summary.Blocks.OfType<CalloutBlock>(), c => c.Kind == CalloutKind.Warning);
        Assert.Contains(model.Sections.SelectMany(s => s.Blocks).OfType<ChartBlock>(), c => c.Title.Contains("Projected value", StringComparison.Ordinal));
    }

    [Fact]
    public void Db_transfer_model_leads_with_the_prescribed_comparator()
    {
        ReportModel model = ReportComposer.Compose(Fixtures.Request(ReportKind.DbTransfer, ReportFormat.Pdf, Fixtures.DbTransfer()), "test");
        Assert.Equal("Transfer Value Comparator", model.Sections[0].Title);
        ParagraphBlock quote = model.Sections[0].Blocks.OfType<ParagraphBlock>().First(p => p.Style == ParagraphStyle.Quote);
        Assert.Contains("cash equivalent transfer value", quote.Text, StringComparison.Ordinal);
        ListBlock notes = model.Sections[0].Blocks.OfType<ListBlock>().First();
        Assert.Equal(3, notes.Items.Count);
        Assert.True(notes.Numbered);
        ChartBlock bars = model.Sections[0].Blocks.OfType<ChartBlock>().First();
        Assert.Contains("Transfer value offered", bars.Svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Suitability_embeds_the_underlying_analysis()
    {
        ReportModel model = ReportComposer.Compose(Fixtures.Request(ReportKind.Suitability, ReportFormat.Pdf, Fixtures.PensionSwitch()), "test");
        List<string> titles = [.. model.Sections.Select(s => s.Title)];
        Assert.Contains("Demands and needs", titles);
        Assert.Contains("Recommendation", titles);
        Assert.Contains("Disadvantages and risks", titles);
        Assert.Contains("Each existing arrangement", titles);
        Assert.Equal("Audit statement", titles[^1]);
        Assert.Contains(model.Sections.SelectMany(s => s.Blocks).OfType<ParagraphBlock>(), p => p.Style == ParagraphStyle.Placeholder);
    }
}

public class SvgChartTests
{
    [Fact]
    public void Line_chart_handles_a_single_point_and_zero_values()
    {
        string single = SvgCharts.Line(new LineChart("t", "x", "y", [new LineSeries("a", [new ChartPoint(0, 0)], ChartPalette.Proposed)]));
        Assert.StartsWith("<svg", single, StringComparison.Ordinal);
        Assert.Contains("</svg>", single, StringComparison.Ordinal);
        string zeros = SvgCharts.Line(new LineChart("t", "x", "y", [new LineSeries("a", [new ChartPoint(0, 0), new ChartPoint(1, 0)], ChartPalette.Proposed)]));
        Assert.Contains("</svg>", zeros, StringComparison.Ordinal);
    }

    [Fact]
    public void Bar_chart_starts_at_zero_and_escapes_labels()
    {
        string svg = SvgCharts.Bars(new BarChart("t", "y", [new BarItem("A & B <test>", 100m, ChartPalette.Proposed), new BarItem("C", 0m, ChartPalette.Existing)]));
        Assert.Contains("&amp;", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<test>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Stacked_area_and_fan_charts_handle_negatives_and_empty_series()
    {
        string area = SvgCharts.StackedArea(new StackedAreaChart("t", "x", "y", [1m, 2m, 3m], [new AreaSeries("a", [10m, -5m, 0m], ChartPalette.Proposed)]));
        Assert.Contains("</svg>", area, StringComparison.Ordinal);
        string fan = SvgCharts.Fan(new FanChart("t", "x", "y", [new FanPoint(1, 0, 0, 0, 0, 0), new FanPoint(2, -10, -5, 0, 5, 10)]));
        Assert.Contains("</svg>", fan, StringComparison.Ordinal);
        string empty = SvgCharts.StackedArea(new StackedAreaChart("t", "x", "y", [], []));
        Assert.Contains("</svg>", empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Money_ticks_are_readable()
    {
        Assert.Equal("£0", SvgCharts.MoneyTick(0));
        Assert.Equal("£1.5k", SvgCharts.MoneyTick(1500));
        Assert.Equal("£2.4m", SvgCharts.MoneyTick(2_400_000));
    }
}
