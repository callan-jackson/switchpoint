using FluentValidation.Results;
using NSubstitute;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Exceptions;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Ports;
using SwitchPoint.Application.Services;
using SwitchPoint.Application.UseCases.Analyses;
using SwitchPoint.Application.Validation;
using SwitchPoint.Calculation.Annuities;
using SwitchPoint.Calculation.Cashflow;
using SwitchPoint.Calculation.CriticalYield;
using SwitchPoint.Calculation.DbTransfer;
using SwitchPoint.Calculation.MonteCarlo;
using SwitchPoint.Calculation.Mortality;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Calculation.Riy;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;
using SwitchPoint.Domain.Tenancy;
using Xunit;

namespace SwitchPoint.Application.Tests;

public class MappingTests
{
    private static ChargeScheduleDto SampleCharges() => new()
    {
        PlatformCharge = new TieredChargeDto(TieredChargeMode.Marginal, [new TierBandDto(250_000m, 0.25m), new TierBandDto(null, 0.15m)]),
        FixedCharges = [new FixedChargeDto(120m, Frequency.Annually, new IndexationDto(IndexationBasis.Cpi, 0m), FixedChargeScope.Drawdown, "Drawdown fee")],
        FundCharge = new FundChargeDto(FundChargeBasisKind.FromHoldings, null),
        TransactionCostsPct = 0.05m,
        AdviserCharges = new AdviserChargeDto(2m, 0m, 0.75m, 0m),
        DealingCharges = new DealingChargesDto(1.5m, 3.95m, 12, 4),
        SwitchCharge = new SwitchChargeDto(0m, 0),
        ExitPenalty = new ExitPenaltyDto([new ExitPenaltyBandDto(5m, 5m, 0m), new ExitPenaltyBandDto(null, 0m, 25m)]),
        BidOfferSpreadPct = 0m,
        AllocationRatePct = 100m,
        LargeFundDiscounts = [new LargeFundDiscountDto(1_000_000m, 0.05m)],
    };

    [Fact]
    public void Pct_conversions_are_exact()
    {
        Assert.Equal(0.0025m, Pct.ToFraction(0.25m));
        Assert.Equal(0.25m, Pct.FromFraction(0.0025m));
        Assert.Null(Pct.ToFraction((decimal?)null));
        Assert.Equal(5m, Pct.FromFraction(0.05m));
    }

    [Fact]
    public void Charge_schedule_round_trips_through_the_domain()
    {
        ChargeScheduleDto dto = SampleCharges();
        ChargeSchedule domain = dto.ToDomain();
        Assert.Equal(0.0025m, domain.PlatformCharge!.Bands[0].AnnualRate);
        Assert.Equal(0.0005m, domain.TransactionCosts);
        Assert.Equal(0.02m, domain.AdviserCharges.InitialRate);
        Assert.Equal(0.05m, domain.ExitPenalty.Bands[0].Rate);
        Assert.Equal(0.0005m, domain.LargeFundDiscounts[0].RebateRate);
        Assert.Equal(domain, domain.ToDto().ToDomain());
        Assert.Equal(JsonDefaults.Serialize(dto), JsonDefaults.Serialize(JsonDefaults.Deserialize<ChargeScheduleDto>(JsonDefaults.Serialize(domain.ToDto()))));
    }

    [Fact]
    public void Guarantees_contributions_holdings_and_index_rules_round_trip()
    {
        GuaranteesDto g = new(9m, null, 40m, 55, true, 2.5m, 1_000m, 0.5m);
        Assert.Equal(g, g.ToDomain().ToDto());
        ContributionDto c = new(ContributionPayer.Member, 200m, Frequency.Monthly, 3m, false, 1, 120);
        Assert.Equal(c, c.ToDomain().ToDto());
        HoldingDto h = new("Vanguard LifeStrategy 60", 60m, "GB00B3TYHH97", null, 0.22m);
        Assert.Equal(h, h.ToDomain().ToDto());
        IndexRuleDto lpi = new(IndexBasis.LpiCpi, 0m, 5m, null);
        Assert.Equal(lpi, lpi.ToRevaluation().ToDto());
        Assert.Equal(lpi, lpi.ToEscalation().ToDto());
        IndexRuleDto fixedRule = new(IndexBasis.Fixed, 4.75m, null, null);
        Assert.Equal(fixedRule, fixedRule.ToRevaluation().ToDto());
    }

    [Fact]
    public void Scheme_write_maps_to_a_defined_benefit_scheme_and_back()
    {
        DateTime now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        SchemeWrite w = new()
        {
            Type = SchemeType.DefinedBenefit,
            ProductName = "ABC Pension Scheme",
            CurrentValue = 450_000m,
            TransferValue = 450_000m,
            ValuationDate = new DateOnly(2026, 8, 1),
            DefinedBenefit = new DefinedBenefitDto(new DateOnly(2016, 3, 31), 65, new DateOnly(2026, 11, 1),
                [new DbTrancheDto("Pre-97 GMP", 1_500m, new IndexRuleDto(IndexBasis.Fixed, 4.75m, null, null), new IndexRuleDto(IndexBasis.None, 0m, null, null), true)],
                50m, 5, 20m, 25m, 4m, 60, 0m, SchemeFundingStatus.FullyFunded),
        };
        Scheme s = w.ToNewScheme(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);
        DefinedBenefitScheme db = Assert.IsType<DefinedBenefitScheme>(s);
        Assert.Equal(0.0475m, db.Tranches[0].Revaluation.Rate);
        Assert.Equal(0.5m, db.SpousePensionFraction);
        SchemeDto dto = s.ToDto(null, new DateOnly(2026, 9, 6), null);
        Assert.Equal(w.DefinedBenefit.NormalRetirementAge, dto.DefinedBenefit!.NormalRetirementAge);
        Assert.Equal(50m, dto.DefinedBenefit.SpousePensionPct);
        Assert.Equal(4.75m, dto.DefinedBenefit.Tranches[0].Revaluation.RatePct);
        Assert.True(dto.DefinedBenefit.Tranches[0].IsGmp);
        Assert.Equal(450_000m, dto.NetTransferValue);
    }

    [Fact]
    public void Client_write_maps_and_masks_ni_number()
    {
        DateTime now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        ClientWrite w = new() { FirstName = "Sam", LastName = "Taylor", DateOfBirth = new DateOnly(1971, 5, 1), Sex = Sex.Female, NationalInsuranceNumber = "QQ123456C", TaxRegime = TaxRegime.Scotland, RiskProfile = 5, StatePension = new StatePensionDto(241.30m, 35) };
        Client c = w.ToNewClient(Guid.NewGuid(), Guid.NewGuid(), now);
        ClientDetail d = c.ToDetail([], new DateOnly(2026, 9, 6));
        Assert.Equal("******56C", d.NationalInsuranceNumberMasked);
        Assert.Null(d.NationalInsuranceNumber);
        Assert.Equal(55, d.Age);
        Assert.Equal(TaxRegime.Scotland, d.TaxRegime);
        Assert.Equal(241.30m, d.StatePension.ForecastWeeklyAmount);
    }

    [Fact]
    public void Product_summary_computes_effective_charges_in_percent()
    {
        DateTime now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        Product p = new(Guid.NewGuid(), Guid.NewGuid(), "Platform SIPP", WrapperTypes.Sipp | WrapperTypes.Isa, now);
        p.AddChargeVersion(new ChargeSchedule { PlatformCharge = TieredCharge.Marginal((250_000m, 0.0025m), (null, 0.0015m)) }, new DateOnly(2026, 1, 1), "https://example.com", new DateOnly(2026, 1, 1), DataQuality.Verified, now);
        ProductSummary s = p.ToSummary("Test Provider");
        Assert.Equal(0.25m, s.EffectiveChargePctAt100k);
        Assert.Equal(0.20m, s.EffectiveChargePctAt500k);
        Assert.Equal(["Sipp", "Isa"], s.WrapperTypes);
        Assert.Equal(WrapperTypes.Sipp | WrapperTypes.Isa, EntityMapping.ParseWrappers(s.WrapperTypes));
    }

    [Fact]
    public void Json_defaults_are_deterministic_and_hash_stable()
    {
        RiyDto dto = new(5m, 0.8m, 1.2m, 4.2m, 3.8m, 100m, 90m, 85m, "p", "t", [], new ChargeTotalsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        string a = JsonDefaults.Serialize(dto);
        string b = JsonDefaults.Serialize(dto with { });
        Assert.Equal(a, b);
        Assert.Equal(JsonDefaults.Sha256(a), JsonDefaults.Sha256(b));
        Assert.Contains("\"growthPct\":5", a, StringComparison.Ordinal);
        Assert.Equal(a, JsonDefaults.Serialize(JsonDefaults.Deserialize<RiyDto>(a)));
    }
}

public class ValidatorTests
{
    [Fact]
    public void Tiered_charge_bands_must_ascend_and_end_unbounded()
    {
        TieredChargeDtoValidator v = new();
        Assert.True(v.Validate(new TieredChargeDto(TieredChargeMode.Marginal, [new TierBandDto(100_000m, 0.3m), new TierBandDto(null, 0.2m)])).IsValid);
        Assert.False(v.Validate(new TieredChargeDto(TieredChargeMode.Marginal, [new TierBandDto(100_000m, 0.3m), new TierBandDto(50_000m, 0.2m), new TierBandDto(null, 0.1m)])).IsValid);
        Assert.False(v.Validate(new TieredChargeDto(TieredChargeMode.Marginal, [new TierBandDto(100_000m, 0.3m)])).IsValid);
        Assert.False(v.Validate(new TieredChargeDto(TieredChargeMode.Marginal, [new TierBandDto(null, 12m)])).IsValid);
    }

    [Fact]
    public void Scheme_holdings_must_sum_to_100_and_have_valid_isins()
    {
        SchemeWriteValidator v = new();
        SchemeWrite good = new() { Type = SchemeType.Sipp, ProductName = "X", CurrentValue = 1m, TransferValue = 1m, ValuationDate = new DateOnly(2026, 9, 1), Holdings = [new HoldingDto("A", 60m, "GB00B3TYHH97", null, null), new HoldingDto("B", 40m, null, null, 0.2m)] };
        Assert.True(v.Validate(good).IsValid);
        ValidationResult bad = v.Validate(good with { Holdings = [new HoldingDto("A", 60m, "GB00B3X7QG64", null, null)] });
        Assert.False(bad.IsValid);
        Assert.Contains(bad.Errors, e => e.ErrorMessage.Contains("100%", StringComparison.Ordinal));
        Assert.Contains(bad.Errors, e => e.ErrorMessage.Contains("ISIN", StringComparison.Ordinal));
        Assert.False(v.Validate(good with { Type = SchemeType.DefinedBenefit }).IsValid);
    }

    [Fact]
    public void Calculation_requests_are_range_checked()
    {
        PensionSwitchCalcRequestValidator v = new();
        PensionSwitchCalcRequest r = new() { CedingSchemes = [new CedingSchemeRef(Guid.NewGuid(), null)], ProposedProductId = Guid.NewGuid(), ProposedHoldings = [new HoldingDto("A", 100m, null, null, 0.2m)], RetirementAge = 67 };
        Assert.True(v.Validate(r).IsValid);
        Assert.False(v.Validate(r with { RetirementAge = 45 }).IsValid);
        Assert.False(v.Validate(r with { CedingSchemes = [] }).IsValid);
        Assert.False(v.Validate(r with { ProposedHoldings = [] }).IsValid);
        Assert.True(v.Validate(r with { ProposedHoldings = [], ProposedModelPortfolioId = Guid.NewGuid() }).IsValid);
        DbTransferAnalysisWriteValidator dv = new();
        DbTransferAnalysisWrite w = new() { ClientId = Guid.NewGuid(), DbSchemeId = Guid.NewGuid(), TransferDate = new DateOnly(2026, 10, 1), AssumptionSetId = Guid.NewGuid(), ChargeBasis = AdviserChargeBasis.Contingent };
        Assert.False(dv.Validate(w).IsValid);
        Assert.True(dv.Validate(w with { ContingentChargingCarveOut = "Serious ill-health" }).IsValid);
    }
}

public class CalculationServiceTests
{
    private static readonly Guid FirmId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    private static (CalculationService Service, IClientRepository Clients, ISchemeRepository Schemes, IProductCatalogue Products, IAssumptionSetRepository Assumptions) Build()
    {
        IClientRepository clients = Substitute.For<IClientRepository>();
        ISchemeRepository schemes = Substitute.For<ISchemeRepository>();
        IProductCatalogue products = Substitute.For<IProductCatalogue>();
        IFundCatalogue funds = Substitute.For<IFundCatalogue>();
        funds.ListByIsinsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns([]);
        IModelPortfolioCatalogue mps = Substitute.For<IModelPortfolioCatalogue>();
        IAssumptionSetRepository assumptions = Substitute.For<IAssumptionSetRepository>();
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.FirmId.Returns(FirmId);
        user.UserId.Returns(Guid.NewGuid());
        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        clock.Today.Returns(DateOnly.FromDateTime(Now));
        ProjectionEngine projection = new();
        ReductionInYieldCalculator riy = new(projection);
        CriticalYieldCalculator cy = new(projection, riy);
        AnnuityPricer pricer = new(GompertzMakehamLifeTable.Default);
        CashflowEngine cashflow = new();
        CalculationService service = new(clients, schemes, products, funds, mps, assumptions, user, clock, projection, riy, cy, new DbTransferCalculator(pricer, projection), cashflow, new MonteCarloSimulator(cashflow));
        return (service, clients, schemes, products, assumptions);
    }

    private static AssumptionSet FcaStandard() => new(Guid.NewGuid(), null, "FCA standard 2026/27", true, Now, 0.02m, 0.05m, 0.08m, 0.02m, 0.035m, 0.03m, 0.02m, 0.004m, 0.04m, 3, MortalityBasis.OnsNationalLifeTables2020_22, 0.035m, "2026/27", ProjectionBasis.Real,
        new MarketInputs(0.042m, 0.044m, 0.046m, 0.047m, 0.008m, 0.040m, 0.010m, new DateOnly(2026, 8, 15)));

    private static Product Platform(Guid id)
    {
        Product p = new(id, Guid.NewGuid(), "Platform SIPP", WrapperTypes.Sipp, Now);
        p.AddChargeVersion(new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.0025m), FundCharge = FundChargeBasis.FromHoldings }, new DateOnly(2026, 1, 1), null, new DateOnly(2026, 1, 1), DataQuality.Verified, Now);
        return p;
    }

    [Fact]
    public async Task Pension_switch_uses_client_age_net_transfer_value_and_percent_conversion()
    {
        (CalculationService service, IClientRepository clients, ISchemeRepository schemes, IProductCatalogue products, IAssumptionSetRepository assumptions) = Build();
        AssumptionSet set = FcaStandard();
        assumptions.ListAsync(FirmId, Arg.Any<CancellationToken>()).Returns([]);
        assumptions.GetFcaStandardAsync(Arg.Any<CancellationToken>()).Returns(set);
        Guid clientId = Guid.NewGuid();
        Client client = new(clientId, FirmId, "A", "B", new DateOnly(1971, 5, 1), Sex.Male, Now); // 55
        clients.GetAsync(FirmId, clientId, Arg.Any<CancellationToken>()).Returns(client);
        Scheme scheme = new(Guid.NewGuid(), FirmId, clientId, SchemeType.PersonalPension, "Legacy PP", 100_000m, 100_000m, new DateOnly(2026, 9, 1), Now);
        scheme.SetProduct(null, "Legacy PP", "P1", new DateOnly(2024, 9, 1), Now);
        scheme.SetCharges(new ChargeSchedule { ProductCharge = TieredCharge.Flat(0.015m), ExitPenalty = ExitPenaltySchedule.Percentage(0.03m, 5m) }, Now);
        schemes.GetAsync(FirmId, scheme.Id, Arg.Any<CancellationToken>()).Returns(scheme);
        Guid productId = Guid.NewGuid();
        products.GetAsync(productId, Arg.Any<CancellationToken>()).Returns(Platform(productId));

        PensionSwitchResultDto r = await service.PensionSwitchAsync(new PensionSwitchCalcRequest
        {
            ClientId = clientId,
            CedingSchemes = [new CedingSchemeRef(scheme.Id, null)],
            ProposedProductId = productId,
            ProposedHoldings = [new HoldingDto("Fund", 100m, null, null, 0.22m)],
            RetirementAge = 67,
        }, CancellationToken.None);

        Assert.Equal(97_000m, r.TotalNetTransferValue);      // 3% exit penalty (2 years in force)
        Assert.Equal(5m, r.Intermediate.GrowthPct);          // percentages in DTOs
        Assert.Equal(2m, r.Lower.GrowthPct);
        Assert.True(r.Intermediate.CriticalYieldPct < 5m);   // cheaper receiving product
        Assert.Equal(set.Id, r.AssumptionSet.Id);
        Assert.Equal(13, r.Chart.Count);                     // year 0 + 12 years
        Assert.Equal(100_000m, r.Chart[0].ExistingValue);
        Assert.Contains("after price inflation", r.ReceivingRiy.ProductSentence, StringComparison.Ordinal);
        Assert.Equal("1.0.0", r.EngineVersion);
    }

    [Fact]
    public async Task Pension_switch_rejects_db_schemes_and_unknown_products()
    {
        (CalculationService service, IClientRepository clients, ISchemeRepository schemes, IProductCatalogue products, IAssumptionSetRepository assumptions) = Build();
        assumptions.ListAsync(FirmId, Arg.Any<CancellationToken>()).Returns([]);
        assumptions.GetFcaStandardAsync(Arg.Any<CancellationToken>()).Returns(FcaStandard());
        Guid clientId = Guid.NewGuid();
        clients.GetAsync(FirmId, clientId, Arg.Any<CancellationToken>()).Returns(new Client(clientId, FirmId, "A", "B", new DateOnly(1971, 5, 1), Sex.Male, Now));
        DefinedBenefitScheme db = new(Guid.NewGuid(), FirmId, clientId, "DB", new DateOnly(2016, 3, 31), 65, 400_000m, new DateOnly(2026, 8, 1), new DateOnly(2026, 11, 1), Now);
        schemes.GetAsync(FirmId, db.Id, Arg.Any<CancellationToken>()).Returns(db);
        Guid productId = Guid.NewGuid();
        products.GetAsync(productId, Arg.Any<CancellationToken>()).Returns(Platform(productId));
        await Assert.ThrowsAsync<ValidationException>(() => service.PensionSwitchAsync(new PensionSwitchCalcRequest { ClientId = clientId, CedingSchemes = [new CedingSchemeRef(db.Id, null)], ProposedProductId = productId, ProposedHoldings = [new HoldingDto("F", 100m, null, null, 0.2m)], RetirementAge = 67 }, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.PensionSwitchAsync(new PensionSwitchCalcRequest { CedingSchemes = [new CedingSchemeRef(Guid.NewGuid(), null)], ProposedProductId = productId, ProposedHoldings = [new HoldingDto("F", 100m, null, null, 0.2m)], RetirementAge = 67 }, CancellationToken.None));
    }

    [Fact]
    public async Task Db_transfer_and_cashflow_run_end_to_end_through_the_service()
    {
        (CalculationService service, IClientRepository clients, ISchemeRepository schemes, IProductCatalogue products, IAssumptionSetRepository assumptions) = Build();
        assumptions.ListAsync(FirmId, Arg.Any<CancellationToken>()).Returns([]);
        assumptions.GetFcaStandardAsync(Arg.Any<CancellationToken>()).Returns(FcaStandard());
        Guid clientId = Guid.NewGuid();
        clients.GetAsync(FirmId, clientId, Arg.Any<CancellationToken>()).Returns(new Client(clientId, FirmId, "A", "B", new DateOnly(1971, 6, 15), Sex.Male, Now));
        DefinedBenefitScheme db = new(Guid.NewGuid(), FirmId, clientId, "DB", new DateOnly(2016, 3, 31), 65, 450_000m, new DateOnly(2026, 8, 1), new DateOnly(2026, 11, 1), Now);
        db.ReplaceTranches([new DbTranche("Post-05", 15_000m, RevaluationRule.StatutoryPost2009, EscalationRule.StatutoryPost2005)], Now);
        schemes.GetAsync(FirmId, db.Id, Arg.Any<CancellationToken>()).Returns(db);
        Guid productId = Guid.NewGuid();
        products.GetAsync(productId, Arg.Any<CancellationToken>()).Returns(Platform(productId));

        DbTransferResultDto d = await service.DbTransferAsync(new DbTransferCalcRequest { DbSchemeId = db.Id, ProposedProductId = productId, ProposedHoldings = [new HoldingDto("F", 100m, null, null, 0.22m)], AptaGrowthPct = 5m, InitialAdviceFee = 9_000m }, CancellationToken.None);
        Assert.Equal(450_000m, d.Tvc.CashEquivalentTransferValue);
        Assert.Equal(4.4m, d.Tvc.GiltYieldUsedPct);
        Assert.True(d.Tvc.EstimatedReplacementCost > 0m);
        Assert.True(d.Summary.PaybackMonths > 0);

        CashflowResultDto c = await service.CashflowAsync(new CashflowCalcRequest
        {
            Person = new PersonDto("A", new DateOnly(1971, 5, 1), Sex.Male, TaxRegime.RestOfUk, 67, 241.30m, 35, false),
            Incomes = [new PlanIncomeDto("Salary", IncomeKind.Employment, 50_000m, 55, null, 3.5m, true, 0)],
            Expenses = [new PlanExpenseDto("Living", 30_000m, 55, null)],
            Assets = [new PlanAssetDto("SIPP", PlanAssetKind.UncrystallisedPension, 300_000m, 5m, ChargeScheduleDto.None, null, 0m, 5_000m, 3_000m, false, 0, null)],
        }, CancellationToken.None);
        Assert.Equal(45, c.Rows.Count);
        Assert.True(c.SustainableSpend > 0m);
        Assert.Equal(5m, Pct.FromFraction(0.05m));

        StochasticResultDto s = await service.StochasticAsync(new CashflowCalcRequest
        {
            Person = new PersonDto("A", new DateOnly(1971, 5, 1), Sex.Male, TaxRegime.RestOfUk, 67, 241.30m, 35, false),
            Expenses = [new PlanExpenseDto("Living", 20_000m, 55, null)],
            Assets = [new PlanAssetDto("SIPP", PlanAssetKind.UncrystallisedPension, 300_000m, 5m, ChargeScheduleDto.None, null, 0m, 0m, 0m, false, 0, new AssetAllocationDto(60m, 40m, 0m, 0m, 0m))],
            Seed = 7,
            Paths = 100,
        }, CancellationToken.None);
        Assert.Equal(100, s.Paths);
        Assert.Equal("7", s.Seed);
        Assert.InRange(s.ProbabilityOfSuccess, 0m, 1m);
    }

    [Fact]
    public void Tax_preview_matches_the_engine()
    {
        (CalculationService service, _, _, _, _) = Build();
        TaxComputationDto t = service.Tax(new TaxCalcRequest(null, TaxRegime.RestOfUk, 20_000m, 0m, 0m, 0m, 0m, 0m, 0m, true));
        Assert.Equal(1_486m, t.IncomeTax);
        Assert.Equal(20m, t.MarginalRatePct);
        Assert.Equal("2026/27", t.TaxYear);
    }
}

public class AnalysisLifecycleHandlerTests
{
    private static readonly Guid FirmId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_calculate_lock_and_delete_rules_with_audit()
    {
        IAnalysisRepository analyses = Substitute.For<IAnalysisRepository>();
        IClientRepository clients = Substitute.For<IClientRepository>();
        ISchemeRepository schemes = Substitute.For<ISchemeRepository>();
        IAssumptionSetRepository sets = Substitute.For<IAssumptionSetRepository>();
        IAuditLog audit = Substitute.For<IAuditLog>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.FirmId.Returns(FirmId);
        user.UserId.Returns(Guid.NewGuid());
        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        clock.Today.Returns(DateOnly.FromDateTime(Now));
        CalculationService calc = new(clients, schemes, Substitute.For<IProductCatalogue>(), Substitute.For<IFundCatalogue>(), Substitute.For<IModelPortfolioCatalogue>(), sets, user, clock,
            new ProjectionEngine(), new ReductionInYieldCalculator(new ProjectionEngine()), new CriticalYieldCalculator(new ProjectionEngine(), new ReductionInYieldCalculator(new ProjectionEngine())), new DbTransferCalculator(new AnnuityPricer(GompertzMakehamLifeTable.Default), new ProjectionEngine()), new CashflowEngine(), new MonteCarloSimulator(new CashflowEngine()));

        Guid clientId = Guid.NewGuid();
        clients.GetAsync(FirmId, clientId, Arg.Any<CancellationToken>()).Returns(new Client(clientId, FirmId, "A", "B", new DateOnly(1971, 5, 1), Sex.Male, Now));
        AssumptionSet set = new(Guid.NewGuid(), null, "FCA", true, Now, 0.02m, 0.05m, 0.08m, 0.02m, 0.035m, 0.03m, 0.02m, 0.004m, 0.04m, 3, MortalityBasis.OnsNationalLifeTables2020_22, 0.035m, "2026/27", ProjectionBasis.Real, new MarketInputs(0.04m, 0.04m, 0.04m, 0.04m, 0.01m, 0.04m, 0.01m, new DateOnly(2026, 8, 1)));
        sets.GetAsync(FirmId, set.Id, Arg.Any<CancellationToken>()).Returns(set);
        PensionSwitchAnalysis? stored = null;
        await analyses.AddAsync(Arg.Do<AnalysisBase>(a => stored = (PensionSwitchAnalysis)a), Arg.Any<CancellationToken>());
        analyses.GetAsync<PensionSwitchAnalysis>(FirmId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => stored);

        AnalysisHandlers handlers = new(analyses, clients, schemes, sets, calc, audit, uow, user, clock);
        PensionSwitchAnalysisDto created = await handlers.CreatePensionSwitchAsync(new PensionSwitchAnalysisWrite { ClientId = clientId, Title = "Switch", RetirementAge = 67, AssumptionSetId = set.Id }, CancellationToken.None);
        Assert.Equal(AnalysisStatus.Draft, created.Status);
        await audit.Received(1).AppendAsync(FirmId, Arg.Any<Guid?>(), "PensionSwitchAnalysis", created.Id, "Created", Arg.Any<object>(), Arg.Any<CancellationToken>());

        // Not ready → validation error; locking a draft → conflict; deleting a draft → allowed.
        await Assert.ThrowsAsync<ValidationException>(() => handlers.CalculatePensionSwitchAsync(created.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => handlers.LockPensionSwitchAsync(created.Id, CancellationToken.None));
        await handlers.DeletePensionSwitchAsync(created.Id, CancellationToken.None);
        await analyses.Received(1).RemoveAsync(Arg.Any<AnalysisBase>(), Arg.Any<CancellationToken>());
    }
}

public class FirmScopingTests
{
    [Fact]
    public async Task Cross_firm_client_is_not_found()
    {
        IClientRepository clients = Substitute.For<IClientRepository>();
        clients.GetAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Client?)null);
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.FirmId.Returns(Guid.NewGuid());
        IClock clock = Substitute.For<IClock>();
        clock.Today.Returns(new DateOnly(2026, 9, 6));
        UseCases.Clients.GetClientHandler handler = new(clients, Substitute.For<ISchemeRepository>(), Substitute.For<IProviderCatalogue>(), Substitute.For<IFundCatalogue>(), Substitute.For<IAuditLog>(), Substitute.For<IUnitOfWork>(), user, clock);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.NotNull(new Firm(Guid.NewGuid(), "Demo", "000000", DateTime.UtcNow));
    }
}
