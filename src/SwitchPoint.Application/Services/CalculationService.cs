using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Exceptions;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Ports;
using SwitchPoint.Calculation.Annuities;
using SwitchPoint.Calculation.Cashflow;
using SwitchPoint.Calculation.CriticalYield;
using SwitchPoint.Calculation.DbTransfer;
using SwitchPoint.Calculation.MonteCarlo;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Calculation.Riy;
using SwitchPoint.Calculation.Tax;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Application.Services;

/// <summary>An assumption set with adviser overrides applied.</summary>
public sealed record ResolvedAssumptions(
    AssumptionSetRefDto Reference,
    decimal GrowthLower,
    decimal GrowthIntermediate,
    decimal GrowthHigher,
    decimal Inflation,
    decimal EarningsGrowth,
    decimal RpiInflation,
    decimal ChargeInflation,
    decimal PreRetirementProductCharge,
    decimal AnnuityExpenseLoading,
    int SpouseAgeGapYears,
    decimal StatePensionIncrease,
    TaxYearParameters Tax,
    MarketInputs MarketInputs,
    ProjectionBasis ProjectionBasis,
    CapitalMarketAssumptions? CapitalMarketAssumptions);

/// <summary>
/// Turns API-shaped calculation requests into engine requests: resolves assumption sets and overrides, loads
/// schemes, products and funds, converts percentages to fractions, runs the engines and maps the results.
/// </summary>
public sealed class CalculationService
{
    private readonly IClientRepository _clients;
    private readonly ISchemeRepository _schemes;
    private readonly IProductCatalogue _products;
    private readonly IFundCatalogue _funds;
    private readonly IModelPortfolioCatalogue _modelPortfolios;
    private readonly IAssumptionSetRepository _assumptions;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly ProjectionEngine _projection;
    private readonly ReductionInYieldCalculator _riy;
    private readonly CriticalYieldCalculator _criticalYield;
    private readonly DbTransferCalculator _dbTransfer;
    private readonly CashflowEngine _cashflow;
    private readonly MonteCarloSimulator _monteCarlo;

    public CalculationService(
        IClientRepository clients,
        ISchemeRepository schemes,
        IProductCatalogue products,
        IFundCatalogue funds,
        IModelPortfolioCatalogue modelPortfolios,
        IAssumptionSetRepository assumptions,
        ICurrentUser user,
        IClock clock,
        ProjectionEngine projection,
        ReductionInYieldCalculator riy,
        CriticalYieldCalculator criticalYield,
        DbTransferCalculator dbTransfer,
        CashflowEngine cashflow,
        MonteCarloSimulator monteCarlo)
    {
        _clients = clients;
        _schemes = schemes;
        _products = products;
        _funds = funds;
        _modelPortfolios = modelPortfolios;
        _assumptions = assumptions;
        _user = user;
        _clock = clock;
        _projection = projection;
        _riy = riy;
        _criticalYield = criticalYield;
        _dbTransfer = dbTransfer;
        _cashflow = cashflow;
        _monteCarlo = monteCarlo;
    }

    // ------------------------------------------------------------------ assumptions

    public async Task<ResolvedAssumptions> ResolveAssumptionsAsync(Guid? assumptionSetId, AssumptionOverrides? overrides, CancellationToken ct)
    {
        AssumptionSet? set = null;
        if (assumptionSetId is { } id)
        {
            set = await _assumptions.GetAsync(_user.FirmId, id, ct) ?? throw new NotFoundException("AssumptionSet", id);
        }

        set ??= (await _assumptions.ListAsync(_user.FirmId, ct)).FirstOrDefault(a => a.FirmId == _user.FirmId)
            ?? await _assumptions.GetFcaStandardAsync(ct)
            ?? throw new NotFoundException("AssumptionSet", "No assumption set is available; seed the FCA standard set.");

        TaxYearParameters tax;
        try
        {
            tax = TaxYears.Get(set.TaxYear);
        }
        catch (KeyNotFoundException)
        {
            tax = TaxYears.For(_clock.Today);
        }

        return new ResolvedAssumptions(
            new AssumptionSetRefDto(set.Id, set.Name, set.Version),
            Pct.ToFraction(overrides?.GrowthLowerPct) ?? set.GrowthLower,
            Pct.ToFraction(overrides?.GrowthIntermediatePct) ?? set.GrowthIntermediate,
            Pct.ToFraction(overrides?.GrowthHigherPct) ?? set.GrowthHigher,
            Pct.ToFraction(overrides?.InflationPct) ?? set.Inflation,
            Pct.ToFraction(overrides?.EarningsGrowthPct) ?? set.EarningsGrowth,
            set.RpiInflation,
            set.ChargeInflation,
            set.PreRetirementProductCharge,
            set.AnnuityExpenseLoading,
            set.SpouseAgeGapYears,
            Pct.ToFraction(overrides?.StatePensionIncreasePct) ?? set.StatePensionIncrease,
            tax,
            set.MarketInputs,
            set.ProjectionBasis,
            set.CapitalMarketAssumptions);
    }

    // ------------------------------------------------------------------ pension switch

    public async Task<PensionSwitchResultDto> PensionSwitchAsync(PensionSwitchCalcRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ResolvedAssumptions a = await ResolveAssumptionsAsync(request.AssumptionSetId, request.Overrides, ct);
        DateOnly today = _clock.Today;

        int age = request.ClientAge ?? 55;
        if (request.ClientId is { } clientId)
        {
            Client client = await _clients.GetAsync(_user.FirmId, clientId, ct) ?? throw new NotFoundException("Client", clientId);
            age = client.AgeOn(today);
        }

        int months = Math.Max(12, (request.RetirementAge - age) * 12);

        // Ceding schemes.
        List<CedingSchemeInput> ceding = [];
        List<(string Name, decimal CurrentValue)> current = [];
        foreach (CedingSchemeRef reference in request.CedingSchemes)
        {
            Scheme scheme;
            string name;
            if (reference.SchemeId is { } schemeId)
            {
                scheme = await _schemes.GetAsync(_user.FirmId, schemeId, ct) ?? throw new NotFoundException("Scheme", schemeId);
                name = scheme.ProductName;
            }
            else if (reference.Inline is { } inline)
            {
                scheme = inline.ToNewScheme(Guid.NewGuid(), _user.FirmId, request.ClientId ?? Guid.NewGuid(), _clock.UtcNow);
                name = inline.Name;
            }
            else
            {
                throw new ValidationException("cedingSchemes", "Each ceding scheme needs a schemeId or an inline definition.");
            }

            if (scheme.Type.HasSafeguardedBenefits())
            {
                throw new ValidationException("cedingSchemes", $"'{name}' has safeguarded benefits; use the DB transfer analysis (COBS 19.1).");
            }

            decimal ocf = await WeightedOcfAsync(scheme.Holdings, ct);
            ProjectionRequest projection = new()
            {
                StartValue = scheme.CurrentValue,
                Months = months,
                GrowthRate = a.GrowthIntermediate,
                Charges = scheme.Charges,
                Contributions = [.. scheme.Contributions.Select(ContributionSpec.From)],
                WeightedOcf = ocf,
                Inflation = a.Inflation,
                ChargeInflation = a.ChargeInflation,
                InDrawdown = scheme.InDrawdown,
                ReliefAtSourceRate = a.Tax.Pensions.ReliefAtSourceRate,
                ApplyInitialAdviserCharge = false,
                YearsInForceAtStart = scheme.YearsInForce(today),
            };
            ceding.Add(new CedingSchemeInput(name, projection, scheme.NetTransferValue(today), scheme.Guarantees));
            current.Add((name, scheme.CurrentValue));
        }

        // Receiving product.
        (ChargeSchedule receivingCharges, decimal receivingOcf) = await ReceivingChargesAsync(request.ProposedProductId, request.ProposedProductChargeVersion, request.ProposedHoldings, request.ProposedModelPortfolioId, request.ProposedAdviserCharges, ct);
        ProjectionRequest template = new()
        {
            StartValue = 0m,
            Months = months,
            GrowthRate = a.GrowthIntermediate,
            Charges = receivingCharges,
            Contributions = request.RedirectContributions ? [] : [new ContributionSpec(0m, Domain.Common.Frequency.Monthly)],
            WeightedOcf = receivingOcf,
            Inflation = a.Inflation,
            ChargeInflation = a.ChargeInflation,
            ReliefAtSourceRate = a.Tax.Pensions.ReliefAtSourceRate,
            ApplyInitialAdviserCharge = true,
        };

        CriticalYieldRequest cyRequest = new(ceding, template, a.GrowthLower, a.GrowthIntermediate, a.GrowthHigher, a.Inflation);
        CriticalYieldResult result = _criticalYield.Calculate(cyRequest);

        // Chart at the intermediate rate.
        List<ProjectionResult> retained = [.. ceding.Select(c => _projection.Project(c.Projection with { GrowthRate = a.GrowthIntermediate }))];
        decimal totalTransfer = ceding.Sum(c => c.NetTransferValue);
        ProjectionResult receiving = _projection.Project(template with
        {
            StartValue = totalTransfer,
            Contributions = request.RedirectContributions ? [.. ceding.SelectMany(c => c.Projection.Contributions)] : template.Contributions,
        });
        IReadOnlyList<ChartPointDto> chart = ResultMapping.BuildChart(retained, receiving, current.Sum(c => c.CurrentValue), totalTransfer - receiving.InitialAdviserCharge);

        return result.ToDto(chart, a.ProjectionBasis == ProjectionBasis.Real, EngineVersion.Current, _clock.UtcNow, a.Reference);
    }

    // ------------------------------------------------------------------ RIY and tax

    public async Task<RiyDto> RiyAsync(RiyCalcRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ChargeSchedule charges = request.Charges.ToDomain();
        decimal? ocf = Pct.ToFraction(request.WeightedOcfPct);
        if (charges.FundCharge.Kind == FundChargeBasisKind.FromHoldings && ocf is null)
        {
            ocf = 0m;
        }

        ProjectionRequest projection = new()
        {
            StartValue = request.StartValue,
            Months = request.Months,
            GrowthRate = Pct.ToFraction(request.GrowthPct),
            Charges = charges,
            Contributions = [.. request.Contributions.Select(c => c.ToSpec())],
            WeightedOcf = ocf,
            Inflation = Pct.ToFraction(request.InflationPct),
            ChargeInflation = Pct.ToFraction(request.InflationPct),
        };
        RiyResult result = _riy.Calculate(projection);
        await Task.CompletedTask;
        return result.ToDto(realTerms: false);
    }

    public TaxComputationDto Tax(TaxCalcRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        TaxYearParameters p = request.TaxYear is null ? TaxYears.For(_clock.Today) : TaxYears.Get(request.TaxYear);
        UkTaxCalculator calc = new(p);
        TaxComputation c = calc.Compute(new TaxableIncome(request.EarnedIncome, request.PensionIncome, request.OtherIncome, request.SavingsInterest, request.Dividends, request.GrossPensionContributions, request.ReliefAtSourceContributions, request.SubjectToNi), request.Regime);
        return c.ToDto();
    }

    // ------------------------------------------------------------------ DB transfer

    public async Task<DbTransferResultDto> DbTransferAsync(DbTransferCalcRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ResolvedAssumptions a = await ResolveAssumptionsAsync(request.AssumptionSetId, request.Overrides, ct);

        DefinedBenefitScheme db;
        Sex sex;
        DateOnly dob;
        if (request.DbSchemeId is { } schemeId)
        {
            Scheme scheme = await _schemes.GetAsync(_user.FirmId, schemeId, ct) ?? throw new NotFoundException("Scheme", schemeId);
            db = scheme as DefinedBenefitScheme ?? throw new ValidationException("dbSchemeId", "The scheme is not a defined benefit scheme.");
            Client client = await _clients.GetAsync(_user.FirmId, scheme.ClientId, ct) ?? throw new NotFoundException("Client", scheme.ClientId);
            sex = client.Sex;
            dob = client.DateOfBirth;
        }
        else if (request.Inline is { } inline)
        {
            Scheme scheme = inline.ToNewScheme(Guid.NewGuid(), _user.FirmId, request.ClientId ?? Guid.NewGuid(), _clock.UtcNow);
            db = scheme as DefinedBenefitScheme ?? throw new ValidationException("inline", "The inline scheme must be of type DefinedBenefit with definedBenefit details.");
            if (request.ClientId is { } clientId)
            {
                Client client = await _clients.GetAsync(_user.FirmId, clientId, ct) ?? throw new NotFoundException("Client", clientId);
                sex = client.Sex;
                dob = client.DateOfBirth;
            }
            else
            {
                sex = request.ClientSex ?? throw new ValidationException("clientSex", "Client sex and date of birth are required for an inline DB analysis.");
                dob = request.ClientDateOfBirth ?? throw new ValidationException("clientDateOfBirth", "Client sex and date of birth are required for an inline DB analysis.");
            }
        }
        else
        {
            throw new ValidationException("dbSchemeId", "A DB scheme id or an inline scheme is required.");
        }

        (ChargeSchedule proposedCharges, decimal proposedOcf) = await ReceivingChargesAsync(request.ProposedProductId, request.ProposedProductChargeVersion, request.ProposedHoldings, null, request.ProposedAdviserCharges, ct);

        DbTransferRequest engineRequest = new()
        {
            Sex = sex,
            DateOfBirth = dob,
            CalculationDate = request.TransferDate ?? _clock.Today,
            DateOfLeaving = db.DateOfLeaving,
            NormalRetirementAge = db.NormalRetirementAge,
            Tranches = [.. db.Tranches.Select(t => new DbTrancheInput(t.Name, t.AccruedAnnualPension, t.Revaluation, t.Escalation, t.IsGmp))],
            CashEquivalentTransferValue = db.CashEquivalentTransferValue,
            SpousePensionFraction = db.SpousePensionFraction,
            GuaranteePeriodYears = db.GuaranteePeriodYears,
            PclsCommutationFactor = db.PclsCommutationFactor,
            MaxPclsFraction = db.MaxPclsFraction,
            EarliestUnreducedAge = db.EarliestUnreducedAge,
            Market = a.MarketInputs,
            RpiAssumption = a.RpiInflation,
            CpiAssumption = a.Inflation,
            EarningsAssumption = a.EarningsGrowth,
            PreRetirementCharge = a.PreRetirementProductCharge,
            AnnuityExpenseLoading = a.AnnuityExpenseLoading,
            SpouseAgeGapYears = a.SpouseAgeGapYears,
            ProposedCharges = proposedCharges,
            ProposedWeightedOcf = proposedOcf,
            AptaGrowthRate = Pct.ToFraction(request.AptaGrowthPct),
            PlanEndAge = request.PlanEndAge,
            InitialAdviceFee = request.InitialAdviceFee,
            WorkplaceDefaultChargeRate = Pct.ToFraction(request.WorkplaceDefaultChargePct),
        };

        DbTransferResult result = _dbTransfer.Calculate(engineRequest);
        return result.ToDto(EngineVersion.Current, _clock.UtcNow, a.Reference);
    }

    // ------------------------------------------------------------------ cashflow

    public async Task<CashflowResultDto> CashflowAsync(CashflowCalcRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ResolvedAssumptions a = await ResolveAssumptionsAsync(request.AssumptionSetId, request.Overrides, ct);
        CashflowRequest engineRequest = BuildCashflowRequest(request, a);
        CashflowResult result = _cashflow.Run(engineRequest);
        decimal sustainable = _cashflow.SustainableSpend(engineRequest);
        return result.ToDto(sustainable, EngineVersion.Current, _clock.UtcNow, a.Reference);
    }

    public async Task<StochasticResultDto> StochasticAsync(CashflowCalcRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ResolvedAssumptions a = await ResolveAssumptionsAsync(request.AssumptionSetId, request.Overrides, ct);
        CashflowRequest engineRequest = BuildCashflowRequest(request, a);
        CapitalMarketAssumptions cma = a.CapitalMarketAssumptions ?? DefaultCapitalMarketAssumptions.Illustrative;
        ulong seed = request.Seed ?? (ulong)(request.ClientId?.GetHashCode() ?? 2026) & 0x7FFFFFFF;
        MonteCarloResult result = _monteCarlo.Run(new MonteCarloRequest(engineRequest, cma, seed, request.Paths ?? 1000, a.Inflation, 0m));
        return result.ToDto(EngineVersion.Current, _clock.UtcNow);
    }

    public CashflowRequest BuildCashflowRequest(CashflowCalcRequest request, ResolvedAssumptions a)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(a);
        static PersonInput Person(PersonDto p) => new(p.Name, p.DateOfBirth, p.Sex, p.TaxRegime, p.RetirementAge, p.StatePensionForecastWeekly, p.StatePensionQualifyingYears, p.MpaaTriggered);
        return new CashflowRequest
        {
            StartDate = _clock.Today,
            Person = Person(request.Person),
            Partner = request.Partner is null ? null : Person(request.Partner),
            PlanEndAge = request.PlanEndAge,
            Incomes = [.. request.Incomes.Select(i => new CashflowIncome(new PlanIncome(i.Name, i.Kind, i.AnnualAmount, i.FromAge, i.ToAge, Pct.ToFraction(i.GrowthPct), i.IsTaxable), i.PersonIndex))],
            Expenses = [.. request.Expenses.Select(e => new PlanExpensePhase(e.Name, e.AnnualAmount, e.FromAge, e.ToAge))],
            Assets = [.. request.Assets.Select(x => new CashflowAsset(new PlanAsset(x.Name, x.Kind, x.Value, Pct.ToFraction(x.GrowthPct), x.Charges.ToDomain(), x.SchemeId, x.CostBasis, x.AnnualContribution, x.EmployerContribution, x.SalarySacrifice), x.PersonIndex, x.Allocation?.ToDomain()))],
            Events = [.. request.Events.Select(e => new PlanEvent(e.Name, e.AtAge, e.Amount))],
            Strategy = new PlanStrategy(request.Strategy.WithdrawalOrder, request.Strategy.Crystallisation, request.Strategy.DrawdownRule, request.Strategy.DrawdownParameter, request.Strategy.ReinvestSurplusIntoIsa, request.Strategy.AnnuityPurchaseAge),
            Tax = a.Tax,
            Inflation = a.Inflation,
            EarningsGrowth = a.EarningsGrowth,
            StatePensionIncrease = a.StatePensionIncrease,
        };
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Charge schedule of the receiving product with the proposed adviser charges substituted, plus the weighted OCF of the proposed investments.</summary>
    public async Task<(ChargeSchedule Charges, decimal WeightedOcf)> ReceivingChargesAsync(Guid productId, int? chargeVersion, IReadOnlyList<HoldingDto> holdings, Guid? modelPortfolioId, AdviserChargeDto adviserCharges, CancellationToken ct)
    {
        Product product = await _products.GetAsync(productId, ct) ?? throw new NotFoundException("Product", productId);
        ProductChargeVersion version = chargeVersion is { } v ? product.Version(v) : product.CurrentCharges ?? throw new ValidationException("proposedProductId", $"Product '{product.Name}' has no charge schedule.");
        ArgumentNullException.ThrowIfNull(adviserCharges);
        ChargeSchedule charges = version.Charges with { AdviserCharges = adviserCharges.ToDomain() };

        decimal ocf;
        if (modelPortfolioId is { } mpsId)
        {
            ModelPortfolio mps = await _modelPortfolios.GetAsync(mpsId, ct) ?? throw new NotFoundException("ModelPortfolio", mpsId);
            ocf = mps.TotalInvestmentCharge;
        }
        else
        {
            ocf = await WeightedOcfAsync([.. holdings.Select(h => h.ToDomain())], ct);
        }

        if (charges.FundCharge.Kind == FundChargeBasisKind.None && ocf > 0m)
        {
            charges = charges with { FundCharge = FundChargeBasis.FromHoldings };
        }

        return (charges, ocf);
    }

    /// <summary>Weighted OCF of holdings, looking up missing OCFs in the fund catalogue by ISIN or id.</summary>
    public async Task<decimal> WeightedOcfAsync(IReadOnlyList<Holding> holdings, CancellationToken ct)
    {
        if (holdings.Count == 0)
        {
            return 0m;
        }

        Dictionary<string, decimal> byIsin = new(StringComparer.OrdinalIgnoreCase);
        List<string> isins = [.. holdings.Where(h => h.Ocf is null && h.Isin is not null).Select(h => h.Isin!)];
        if (isins.Count > 0)
        {
            foreach (Fund f in await _funds.ListByIsinsAsync(isins, ct))
            {
                byIsin[f.Isin] = f.Ocf;
            }
        }

        Dictionary<Guid, decimal> byId = [];
        foreach (Holding h in holdings.Where(h => h.Ocf is null && h.Isin is null && h.FundId is not null))
        {
            Fund? f = await _funds.GetAsync(h.FundId!.Value, ct);
            if (f is not null)
            {
                byId[f.Id] = f.Ocf;
            }
        }

        return Holding.WeightedOcf(holdings, h =>
        {
            if (h.Isin is not null && byIsin.TryGetValue(h.Isin, out decimal o))
            {
                return o;
            }

            if (h.FundId is { } id && byId.TryGetValue(id, out decimal o2))
            {
                return o2;
            }

            throw new ValidationException("holdings", $"No OCF is known for holding '{h.Name}'; supply ocfPct or a catalogued ISIN.");
        });
    }
}

/// <summary>Illustrative capital market assumptions used when an assumption set carries none (documented in docs/methodology/monte-carlo.md).</summary>
public static class DefaultCapitalMarketAssumptions
{
    public static CapitalMarketAssumptions Illustrative { get; } = new(
        [
            new AssetClassAssumption(AssetClass.UkEquity, 0.065m, 0.16m),
            new AssetClassAssumption(AssetClass.GlobalEquity, 0.065m, 0.15m),
            new AssetClassAssumption(AssetClass.GovernmentBonds, 0.035m, 0.07m),
            new AssetClassAssumption(AssetClass.CorporateBonds, 0.045m, 0.08m),
            new AssetClassAssumption(AssetClass.Property, 0.045m, 0.12m),
            new AssetClassAssumption(AssetClass.Cash, 0.03m, 0.01m),
            new AssetClassAssumption(AssetClass.Alternatives, 0.05m, 0.10m),
        ],
        new decimal[,]
        {
            { 1.0m, 0.8m, 0.1m, 0.3m, 0.4m, 0.0m, 0.4m },
            { 0.8m, 1.0m, 0.1m, 0.3m, 0.4m, 0.0m, 0.4m },
            { 0.1m, 0.1m, 1.0m, 0.7m, 0.1m, 0.2m, 0.1m },
            { 0.3m, 0.3m, 0.7m, 1.0m, 0.2m, 0.1m, 0.2m },
            { 0.4m, 0.4m, 0.1m, 0.2m, 1.0m, 0.0m, 0.3m },
            { 0.0m, 0.0m, 0.2m, 0.1m, 0.0m, 1.0m, 0.0m },
            { 0.4m, 0.4m, 0.1m, 0.2m, 0.3m, 0.0m, 1.0m },
        },
        new DateOnly(2026, 1, 1),
        "SwitchPoint illustrative long-term assumptions (not a licensed CMA)");
}
