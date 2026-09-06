using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Exceptions;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Ports;
using SwitchPoint.Application.Services;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Application.UseCases.Analyses;

/// <summary>Stateless calculation endpoints (live preview). Results are not persisted; the call is audited as a preview.</summary>
public sealed class PreviewCalculationHandler(CalculationService calculations, IAuditLog audit, IUnitOfWork uow, ICurrentUser user)
{
    public async Task<PensionSwitchResultDto> PensionSwitchAsync(PensionSwitchCalcRequest request, CancellationToken ct)
    {
        PensionSwitchResultDto result = await calculations.PensionSwitchAsync(request, ct);
        await PreviewAuditAsync("PensionSwitch", request.ClientId, new { request.ProposedProductId, request.RetirementAge, CriticalYieldPct = result.Intermediate.CriticalYieldPct }, ct);
        return result;
    }

    public async Task<DbTransferResultDto> DbTransferAsync(DbTransferCalcRequest request, CancellationToken ct)
    {
        DbTransferResultDto result = await calculations.DbTransferAsync(request, ct);
        await PreviewAuditAsync("DbTransfer", request.ClientId, new { request.DbSchemeId, request.ProposedProductId, Tvc = result.Tvc.EstimatedReplacementCost }, ct);
        return result;
    }

    public async Task<CashflowResultDto> CashflowAsync(CashflowCalcRequest request, CancellationToken ct)
    {
        CashflowResultDto result = await calculations.CashflowAsync(request, ct);
        await PreviewAuditAsync("Cashflow", request.ClientId, new { request.PlanEndAge, result.FirstShortfallAge }, ct);
        return result;
    }

    public async Task<StochasticResultDto> StochasticAsync(CashflowCalcRequest request, CancellationToken ct)
    {
        StochasticResultDto result = await calculations.StochasticAsync(request, ct);
        await PreviewAuditAsync("CashflowStochastic", request.ClientId, new { result.Seed, result.Paths, result.ProbabilityOfSuccess }, ct);
        return result;
    }

    public Task<RiyDto> RiyAsync(RiyCalcRequest request, CancellationToken ct) => calculations.RiyAsync(request, ct);

    public TaxComputationDto Tax(TaxCalcRequest request) => calculations.Tax(request);

    private async Task PreviewAuditAsync(string kind, Guid? clientId, object payload, CancellationToken ct)
    {
        await audit.AppendAsync(user.FirmId, user.UserId, "Calculation", clientId, $"Preview:{kind}", payload, ct);
        await uow.SaveChangesAsync(ct);
    }
}

/// <summary>Create / read / update / calculate / lock / delete for persisted analyses of all three kinds.</summary>
public sealed class AnalysisHandlers
{
    private readonly IAnalysisRepository _analyses;
    private readonly IClientRepository _clients;
    private readonly ISchemeRepository _schemes;
    private readonly IAssumptionSetRepository _assumptionSets;
    private readonly CalculationService _calculations;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public AnalysisHandlers(IAnalysisRepository analyses, IClientRepository clients, ISchemeRepository schemes, IAssumptionSetRepository assumptionSets, CalculationService calculations, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    {
        _analyses = analyses;
        _clients = clients;
        _schemes = schemes;
        _assumptionSets = assumptionSets;
        _calculations = calculations;
        _audit = audit;
        _uow = uow;
        _user = user;
        _clock = clock;
    }

    // ---------------------------------------------------------------- pension switch

    public async Task<PensionSwitchAnalysisDto> CreatePensionSwitchAsync(PensionSwitchAnalysisWrite w, CancellationToken ct)
    {
        await RequireClientAsync(w.ClientId, ct);
        await RequireAssumptionSetAsync(w.AssumptionSetId, ct);
        PensionSwitchAnalysis a = new(Guid.NewGuid(), _user.FirmId, w.ClientId, w.AssumptionSetId, _user.UserId, w.Title, w.RetirementAge, _clock.UtcNow);
        await ApplyAsync(a, w, ct);
        await _analyses.AddAsync(a, ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, "PensionSwitchAnalysis", a.Id, "Created", new { a.Id, a.ClientId, a.Title }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task<PensionSwitchAnalysisDto> GetPensionSwitchAsync(Guid id, CancellationToken ct) => ToDto(await RequireAsync<PensionSwitchAnalysis>(id, ct));

    public async Task<PensionSwitchAnalysisDto> UpdatePensionSwitchAsync(Guid id, PensionSwitchAnalysisWrite w, CancellationToken ct)
    {
        PensionSwitchAnalysis a = await RequireAsync<PensionSwitchAnalysis>(id, ct);
        EnsureEditable(a);
        if (a.ClientId != w.ClientId)
        {
            throw new ConflictException("An analysis cannot be moved to another client.");
        }

        a.Rename(w.Title, _clock.UtcNow);
        a.SetRetirementAge(w.RetirementAge, _clock.UtcNow);
        await ApplyAsync(a, w, ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, "PensionSwitchAnalysis", a.Id, "Updated", new { a.Id, a.Title, a.RetirementAge, a.ProposedProductId }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task<PensionSwitchAnalysisDto> CalculatePensionSwitchAsync(Guid id, CancellationToken ct)
    {
        PensionSwitchAnalysis a = await RequireAsync<PensionSwitchAnalysis>(id, ct);
        EnsureEditable(a);
        if (!a.IsReadyToCalculate)
        {
            throw new ValidationException("analysis", "Choose at least one ceding scheme and a proposed product before calculating.");
        }

        PensionSwitchCalcRequest request = new()
        {
            ClientId = a.ClientId,
            CedingSchemes = [.. a.CedingSchemeIds.Select(s => new CedingSchemeRef(s, null))],
            ProposedProductId = a.ProposedProductId!.Value,
            ProposedProductChargeVersion = a.ProposedProductChargeVersion,
            ProposedHoldings = [.. a.ProposedHoldings.Select(h => h.ToDto())],
            ProposedModelPortfolioId = a.ProposedModelPortfolioId,
            ProposedAdviserCharges = a.ProposedAdviserCharges.ToDto(),
            RetirementAge = a.RetirementAge,
            AssumptionSetId = a.AssumptionSetId,
            Overrides = ParseOverrides(a.OverridesJson),
        };
        PensionSwitchResultDto result = await _calculations.PensionSwitchAsync(request, ct);
        Record(a, result, ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, "PensionSwitchAnalysis", a.Id, "Calculated", new { a.Id, a.Version, a.ResultHash, result.Intermediate.CriticalYieldPct, result.Intermediate.HeadroomPct }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task<PensionSwitchAnalysisDto> LockPensionSwitchAsync(Guid id, CancellationToken ct) => ToDto(await LockAsync<PensionSwitchAnalysis>(id, "PensionSwitchAnalysis", ct));

    public Task DeletePensionSwitchAsync(Guid id, CancellationToken ct) => DeleteAsync<PensionSwitchAnalysis>(id, "PensionSwitchAnalysis", ct);

    private async Task ApplyAsync(PensionSwitchAnalysis a, PensionSwitchAnalysisWrite w, CancellationToken ct)
    {
        a.SetAssumptions(w.AssumptionSetId, (await RequireAssumptionSetAsync(w.AssumptionSetId, ct)).Version, w.Overrides is null ? null : JsonDefaults.Serialize(w.Overrides), _clock.UtcNow);
        if (w.CedingSchemeIds.Count > 0)
        {
            IReadOnlyList<Scheme> schemes = await _schemes.ListAsync(_user.FirmId, w.CedingSchemeIds, ct);
            foreach (Guid sid in w.CedingSchemeIds)
            {
                Scheme s = schemes.FirstOrDefault(x => x.Id == sid) ?? throw new NotFoundException("Scheme", sid);
                if (s.ClientId != a.ClientId)
                {
                    throw new ValidationException("cedingSchemeIds", $"Scheme {sid} does not belong to this client.");
                }
            }

            a.SetCedingSchemes(w.CedingSchemeIds, _clock.UtcNow);
        }

        if (w.ProposedProductId is { } pid)
        {
            a.SetProposal(pid, w.ProposedProductChargeVersion ?? 1, w.ProposedHoldings.Select(h => h.ToDomain()), w.ProposedModelPortfolioId, w.ProposedAdviserCharges.ToDomain(), _clock.UtcNow);
        }

        a.SetRationale(w.Rationale, _clock.UtcNow);
    }

    private static PensionSwitchAnalysisDto ToDto(PensionSwitchAnalysis a) => new()
    {
        Id = a.Id,
        FirmId = a.FirmId,
        ClientId = a.ClientId,
        Title = a.Title,
        RetirementAge = a.RetirementAge,
        CedingSchemeIds = a.CedingSchemeIds,
        ProposedProductId = a.ProposedProductId,
        ProposedProductChargeVersion = a.ProposedProductChargeVersion,
        ProposedHoldings = [.. a.ProposedHoldings.Select(h => h.ToDto())],
        ProposedModelPortfolioId = a.ProposedModelPortfolioId,
        ProposedAdviserCharges = a.ProposedAdviserCharges.ToDto(),
        AssumptionSetId = a.AssumptionSetId,
        Overrides = ParseOverrides(a.OverridesJson),
        Rationale = a.Rationale,
        Status = a.Status,
        Version = a.Version,
        ResultHash = a.ResultHash,
        CalculatedAtUtc = a.CalculatedAtUtc,
        EngineVersion = a.EngineVersion,
        Result = a.ResultJson is null ? null : JsonDefaults.Deserialize<PensionSwitchResultDto>(a.ResultJson),
        CreatedAtUtc = a.CreatedAtUtc,
        UpdatedAtUtc = a.UpdatedAtUtc,
        CreatedBy = a.CreatedBy,
    };

    // ---------------------------------------------------------------- DB transfer

    public async Task<DbTransferAnalysisDto> CreateDbTransferAsync(DbTransferAnalysisWrite w, CancellationToken ct)
    {
        await RequireClientAsync(w.ClientId, ct);
        Scheme scheme = await _schemes.GetAsync(_user.FirmId, w.DbSchemeId, ct) ?? throw new NotFoundException("Scheme", w.DbSchemeId);
        if (scheme is not DefinedBenefitScheme || scheme.ClientId != w.ClientId)
        {
            throw new ValidationException("dbSchemeId", "The scheme must be a defined benefit scheme belonging to this client.");
        }

        DbTransferAnalysis a = new(Guid.NewGuid(), _user.FirmId, w.ClientId, w.DbSchemeId, w.AssumptionSetId, _user.UserId, w.TransferDate, _clock.UtcNow);
        await ApplyAsync(a, w, ct);
        await _analyses.AddAsync(a, ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, "DbTransferAnalysis", a.Id, "Created", new { a.Id, a.ClientId, a.DbSchemeId }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task<DbTransferAnalysisDto> GetDbTransferAsync(Guid id, CancellationToken ct) => ToDto(await RequireAsync<DbTransferAnalysis>(id, ct));

    public async Task<DbTransferAnalysisDto> UpdateDbTransferAsync(Guid id, DbTransferAnalysisWrite w, CancellationToken ct)
    {
        DbTransferAnalysis a = await RequireAsync<DbTransferAnalysis>(id, ct);
        EnsureEditable(a);
        if (a.ClientId != w.ClientId || a.DbSchemeId != w.DbSchemeId)
        {
            throw new ConflictException("The client and DB scheme of an analysis cannot be changed.");
        }

        await ApplyAsync(a, w, ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, "DbTransferAnalysis", a.Id, "Updated", new { a.Id, a.ProposedProductId, a.ChargeBasis }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task<DbTransferAnalysisDto> CalculateDbTransferAsync(Guid id, CancellationToken ct)
    {
        DbTransferAnalysis a = await RequireAsync<DbTransferAnalysis>(id, ct);
        EnsureEditable(a);
        if (!a.IsReadyToCalculate)
        {
            throw new ValidationException("analysis", "Choose a proposed product, holdings and an APTA growth rate before calculating.");
        }

        DbTransferInputs inputs = ParseExtras(a.OverridesJson);
        DbTransferCalcRequest request = new()
        {
            ClientId = a.ClientId,
            DbSchemeId = a.DbSchemeId,
            ProposedProductId = a.ProposedProductId!.Value,
            ProposedProductChargeVersion = a.ProposedProductChargeVersion,
            ProposedHoldings = [.. a.ProposedHoldings.Select(h => h.ToDto())],
            ProposedAdviserCharges = a.ProposedAdviserCharges.ToDto(),
            AptaGrowthPct = Pct.FromFraction(a.AptaGrowthRate!.Value),
            PlanEndAge = a.PlanEndAge,
            InitialAdviceFee = inputs.InitialAdviceFee,
            WorkplaceDefaultChargePct = inputs.WorkplaceDefaultChargePct,
            AssumptionSetId = a.AssumptionSetId,
            Overrides = inputs.Overrides,
            TransferDate = a.TransferDate,
        };
        DbTransferResultDto result = await _calculations.DbTransferAsync(request, ct);
        Record(a, result, ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, "DbTransferAnalysis", a.Id, "Calculated", new { a.Id, a.Version, a.ResultHash, result.Tvc.EstimatedReplacementCost, result.Tvc.CashEquivalentTransferValue }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task<DbTransferAnalysisDto> LockDbTransferAsync(Guid id, CancellationToken ct) => ToDto(await LockAsync<DbTransferAnalysis>(id, "DbTransferAnalysis", ct));

    public Task DeleteDbTransferAsync(Guid id, CancellationToken ct) => DeleteAsync<DbTransferAnalysis>(id, "DbTransferAnalysis", ct);

    private async Task ApplyAsync(DbTransferAnalysis a, DbTransferAnalysisWrite w, CancellationToken ct)
    {
        DbTransferInputs extras = new(w.InitialAdviceFee, w.WorkplaceDefaultChargePct, w.Overrides);
        a.SetAssumptions(w.AssumptionSetId, (await RequireAssumptionSetAsync(w.AssumptionSetId, ct)).Version, JsonDefaults.Serialize(extras), _clock.UtcNow);
        a.SetTransferDate(w.TransferDate, w.PlanEndAge, _clock.UtcNow);
        a.SetChargeBasis(w.ChargeBasis, w.ContingentChargingCarveOut, _clock.UtcNow);
        a.SetWorkplaceComparison(w.WorkplaceSchemeProductId, _clock.UtcNow);
        if (w.ProposedProductId is { } pid && w.AptaGrowthPct is { } g)
        {
            a.SetProposal(pid, w.ProposedProductChargeVersion ?? 1, w.ProposedHoldings.Select(h => h.ToDomain()), w.ProposedAdviserCharges.ToDomain(), Pct.ToFraction(g), _clock.UtcNow);
        }
    }

    private static DbTransferAnalysisDto ToDto(DbTransferAnalysis a)
    {
        DbTransferInputs extras = ParseExtras(a.OverridesJson);
        return new DbTransferAnalysisDto
        {
            Id = a.Id,
            FirmId = a.FirmId,
            ClientId = a.ClientId,
            DbSchemeId = a.DbSchemeId,
            TransferDate = a.TransferDate,
            PlanEndAge = a.PlanEndAge,
            ProposedProductId = a.ProposedProductId,
            ProposedProductChargeVersion = a.ProposedProductChargeVersion,
            ProposedHoldings = [.. a.ProposedHoldings.Select(h => h.ToDto())],
            ProposedAdviserCharges = a.ProposedAdviserCharges.ToDto(),
            AptaGrowthPct = Pct.FromFraction(a.AptaGrowthRate),
            ChargeBasis = a.ChargeBasis,
            ContingentChargingCarveOut = a.ContingentChargingCarveOut,
            WorkplaceSchemeProductId = a.WorkplaceSchemeProductId,
            InitialAdviceFee = extras.InitialAdviceFee,
            WorkplaceDefaultChargePct = extras.WorkplaceDefaultChargePct,
            AssumptionSetId = a.AssumptionSetId,
            Overrides = extras.Overrides,
            Status = a.Status,
            Version = a.Version,
            ResultHash = a.ResultHash,
            CalculatedAtUtc = a.CalculatedAtUtc,
            EngineVersion = a.EngineVersion,
            Result = a.ResultJson is null ? null : JsonDefaults.Deserialize<DbTransferResultDto>(a.ResultJson),
            CreatedAtUtc = a.CreatedAtUtc,
            UpdatedAtUtc = a.UpdatedAtUtc,
            CreatedBy = a.CreatedBy,
        };
    }

    /// <summary>Extra DB inputs that have no dedicated column; stored in the analysis's overrides JSON.</summary>
    private sealed record DbTransferInputs(decimal InitialAdviceFee, decimal? WorkplaceDefaultChargePct, AssumptionOverrides? Overrides);

    private static DbTransferInputs ParseExtras(string? json) => json is null ? new DbTransferInputs(0m, null, null) : JsonDefaults.Deserialize<DbTransferInputs>(json) ?? new DbTransferInputs(0m, null, null);

    // ---------------------------------------------------------------- cashflow

    public async Task<CashflowPlanDto> CreateCashflowAsync(CashflowPlanWrite w, CancellationToken ct)
    {
        await RequireClientAsync(w.ClientId, ct);
        await RequireAssumptionSetAsync(w.AssumptionSetId, ct);
        CashflowPlan p = new(Guid.NewGuid(), _user.FirmId, w.ClientId, w.AssumptionSetId, _user.UserId, w.Title, _clock.UtcNow, w.PartnerClientId, w.PlanEndAge);
        await ApplyAsync(p, w, ct);
        await _analyses.AddAsync(p, ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, "CashflowPlan", p.Id, "Created", new { p.Id, p.ClientId, p.Title }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<CashflowPlanDto> GetCashflowAsync(Guid id, CancellationToken ct) => ToDto(await RequireAsync<CashflowPlan>(id, ct));

    public async Task<CashflowPlanDto> UpdateCashflowAsync(Guid id, CashflowPlanWrite w, CancellationToken ct)
    {
        CashflowPlan p = await RequireAsync<CashflowPlan>(id, ct);
        EnsureEditable(p);
        if (p.ClientId != w.ClientId)
        {
            throw new ConflictException("A plan cannot be moved to another client.");
        }

        p.Rename(w.Title, _clock.UtcNow);
        await ApplyAsync(p, w, ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, "CashflowPlan", p.Id, "Updated", new { p.Id, p.Title, p.PlanEndAge }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<CashflowPlanDto> CalculateCashflowAsync(Guid id, bool stochastic, CancellationToken ct)
    {
        CashflowPlan p = await RequireAsync<CashflowPlan>(id, ct);
        EnsureEditable(p);
        CashflowCalcRequest request = await BuildRequestAsync(p, ct);
        CashflowStoredResult stored = p.ResultJson is null ? new CashflowStoredResult(null, null) : JsonDefaults.Deserialize<CashflowStoredResult>(p.ResultJson) ?? new CashflowStoredResult(null, null);
        CashflowResultDto deterministic = await _calculations.CashflowAsync(request, ct);
        StochasticResultDto? stochasticResult = stochastic ? await _calculations.StochasticAsync(request with { Seed = p.StochasticSeed, Paths = p.StochasticPaths }, ct) : stored.Stochastic;
        Record(p, new CashflowStoredResult(deterministic, stochasticResult), ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, "CashflowPlan", p.Id, stochastic ? "CalculatedStochastic" : "Calculated", new { p.Id, p.Version, p.ResultHash, deterministic.FirstShortfallAge, ProbabilityOfSuccess = stochasticResult?.ProbabilityOfSuccess }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<CashflowPlanDto> LockCashflowAsync(Guid id, CancellationToken ct) => ToDto(await LockAsync<CashflowPlan>(id, "CashflowPlan", ct));

    public Task DeleteCashflowAsync(Guid id, CancellationToken ct) => DeleteAsync<CashflowPlan>(id, "CashflowPlan", ct);

    private async Task ApplyAsync(CashflowPlan p, CashflowPlanWrite w, CancellationToken ct)
    {
        p.SetHorizon(w.PlanEndAge, w.PartnerClientId, _clock.UtcNow);
        p.ReplaceIncomes(w.Incomes.Select(i => new PlanIncome(i.Name, i.Kind, i.AnnualAmount, i.FromAge, i.ToAge, Pct.ToFraction(i.GrowthPct), i.IsTaxable)), _clock.UtcNow);
        p.ReplaceExpenses(w.Expenses.Select(e => new PlanExpensePhase(e.Name, e.AnnualAmount, e.FromAge, e.ToAge)), _clock.UtcNow);
        p.ReplaceAssets(w.Assets.Select(a => new PlanAsset(a.Name, a.Kind, a.Value, Pct.ToFraction(a.GrowthPct), a.Charges.ToDomain(), a.SchemeId, a.CostBasis, a.AnnualContribution, a.EmployerContribution, a.SalarySacrifice)), _clock.UtcNow);
        p.ReplaceEvents(w.Events.Select(e => new PlanEvent(e.Name, e.AtAge, e.Amount)), _clock.UtcNow);
        p.SetStrategy(new PlanStrategy(w.Strategy.WithdrawalOrder, w.Strategy.Crystallisation, w.Strategy.DrawdownRule, w.Strategy.DrawdownParameter, w.Strategy.ReinvestSurplusIntoIsa, w.Strategy.AnnuityPurchaseAge), _clock.UtcNow);
        p.SetStochasticSettings(w.StochasticSeed ?? (ulong)(p.Id.GetHashCode() & 0x7FFFFFFF), w.StochasticPaths, _clock.UtcNow);
        // Person indices, allocations and partner details live in the overrides JSON alongside the assumption overrides.
        CashflowExtras extras = new(w.Incomes.Select(i => i.PersonIndex).ToList(), w.Assets.Select(a => a.PersonIndex).ToList(), w.Assets.Select(a => a.Allocation).ToList(), w.Overrides);
        p.SetAssumptions(w.AssumptionSetId, (await RequireAssumptionSetAsync(w.AssumptionSetId, ct)).Version, JsonDefaults.Serialize(extras), _clock.UtcNow);
    }

    private async Task<CashflowCalcRequest> BuildRequestAsync(CashflowPlan p, CancellationToken ct)
    {
        Domain.Clients.Client client = await RequireClientAsync(p.ClientId, ct);
        Domain.Clients.Client? partner = p.PartnerClientId is { } pid ? await _clients.GetAsync(_user.FirmId, pid, ct) : null;
        CashflowExtras extras = p.OverridesJson is null ? new CashflowExtras([], [], [], null) : JsonDefaults.Deserialize<CashflowExtras>(p.OverridesJson) ?? new CashflowExtras([], [], [], null);
        static PersonDto Person(Domain.Clients.Client c) => new(c.FullName, c.DateOfBirth, c.Sex, c.TaxRegime, c.TargetRetirementAge, c.StatePension.ForecastWeeklyAmount, c.StatePension.QualifyingYears, false);
        return new CashflowCalcRequest
        {
            ClientId = p.ClientId,
            Person = Person(client),
            Partner = partner is null ? null : Person(partner),
            PlanEndAge = p.PlanEndAge,
            Incomes = [.. p.Incomes.Select((i, idx) => new PlanIncomeDto(i.Name, i.Kind, i.AnnualAmount, i.FromAge, i.ToAge, Pct.FromFraction(i.GrowthRate), i.IsTaxable, idx < extras.IncomePersons.Count ? extras.IncomePersons[idx] : 0))],
            Expenses = [.. p.Expenses.Select(e => new PlanExpenseDto(e.Name, e.AnnualAmount, e.FromAge, e.ToAge))],
            Assets = [.. p.Assets.Select((a, idx) => new PlanAssetDto(a.Name, a.Kind, a.Value, Pct.FromFraction(a.GrowthRate), a.Charges.ToDto(), a.SchemeId, a.CostBasis, a.AnnualContribution, a.EmployerContribution, a.SalarySacrifice, idx < extras.AssetPersons.Count ? extras.AssetPersons[idx] : 0, idx < extras.Allocations.Count ? extras.Allocations[idx] : null))],
            Events = [.. p.Events.Select(e => new PlanEventDto(e.Name, e.AtAge, e.Amount))],
            Strategy = new PlanStrategyDto(p.Strategy.WithdrawalOrder, p.Strategy.Crystallisation, p.Strategy.DrawdownRule, p.Strategy.DrawdownParameter, p.Strategy.ReinvestSurplusIntoIsa, p.Strategy.AnnuityPurchaseAge),
            AssumptionSetId = p.AssumptionSetId,
            Overrides = extras.Overrides,
            Seed = p.StochasticSeed,
            Paths = p.StochasticPaths,
        };
    }

    private static CashflowPlanDto ToDto(CashflowPlan p)
    {
        CashflowExtras extras = p.OverridesJson is null ? new CashflowExtras([], [], [], null) : JsonDefaults.Deserialize<CashflowExtras>(p.OverridesJson) ?? new CashflowExtras([], [], [], null);
        CashflowStoredResult stored = p.ResultJson is null ? new CashflowStoredResult(null, null) : JsonDefaults.Deserialize<CashflowStoredResult>(p.ResultJson) ?? new CashflowStoredResult(null, null);
        return new CashflowPlanDto
        {
            Id = p.Id,
            FirmId = p.FirmId,
            ClientId = p.ClientId,
            PartnerClientId = p.PartnerClientId,
            Title = p.Title,
            PlanEndAge = p.PlanEndAge,
            Incomes = [.. p.Incomes.Select((i, idx) => new PlanIncomeDto(i.Name, i.Kind, i.AnnualAmount, i.FromAge, i.ToAge, Pct.FromFraction(i.GrowthRate), i.IsTaxable, idx < extras.IncomePersons.Count ? extras.IncomePersons[idx] : 0))],
            Expenses = [.. p.Expenses.Select(e => new PlanExpenseDto(e.Name, e.AnnualAmount, e.FromAge, e.ToAge))],
            Assets = [.. p.Assets.Select((a, idx) => new PlanAssetDto(a.Name, a.Kind, a.Value, Pct.FromFraction(a.GrowthRate), a.Charges.ToDto(), a.SchemeId, a.CostBasis, a.AnnualContribution, a.EmployerContribution, a.SalarySacrifice, idx < extras.AssetPersons.Count ? extras.AssetPersons[idx] : 0, idx < extras.Allocations.Count ? extras.Allocations[idx] : null))],
            Events = [.. p.Events.Select(e => new PlanEventDto(e.Name, e.AtAge, e.Amount))],
            Strategy = new PlanStrategyDto(p.Strategy.WithdrawalOrder, p.Strategy.Crystallisation, p.Strategy.DrawdownRule, p.Strategy.DrawdownParameter, p.Strategy.ReinvestSurplusIntoIsa, p.Strategy.AnnuityPurchaseAge),
            StochasticSeed = p.StochasticSeed,
            StochasticPaths = p.StochasticPaths,
            AssumptionSetId = p.AssumptionSetId,
            Overrides = extras.Overrides,
            Status = p.Status,
            Version = p.Version,
            ResultHash = p.ResultHash,
            CalculatedAtUtc = p.CalculatedAtUtc,
            EngineVersion = p.EngineVersion,
            Result = stored.Deterministic,
            StochasticResult = stored.Stochastic,
            CreatedAtUtc = p.CreatedAtUtc,
            UpdatedAtUtc = p.UpdatedAtUtc,
            CreatedBy = p.CreatedBy,
        };
    }

    private sealed record CashflowExtras(IReadOnlyList<int> IncomePersons, IReadOnlyList<int> AssetPersons, IReadOnlyList<AssetAllocationDto?> Allocations, AssumptionOverrides? Overrides);

    private sealed record CashflowStoredResult(CashflowResultDto? Deterministic, StochasticResultDto? Stochastic);

    // ---------------------------------------------------------------- shared

    /// <summary>Serialises and hashes a result deterministically and records it on the analysis.</summary>
    private void Record<T>(AnalysisBase a, T result, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string json = JsonDefaults.Serialize(result);
        a.RecordResult(json, JsonDefaults.Sha256(json), EngineVersion.Current, _clock.UtcNow);
    }

    private async Task<T> RequireAsync<T>(Guid id, CancellationToken ct) where T : AnalysisBase =>
        await _analyses.GetAsync<T>(_user.FirmId, id, ct) ?? throw new NotFoundException(typeof(T).Name, id);

    private async Task<Domain.Clients.Client> RequireClientAsync(Guid clientId, CancellationToken ct) =>
        await _clients.GetAsync(_user.FirmId, clientId, ct) ?? throw new NotFoundException("Client", clientId);

    private async Task<Domain.Assumptions.AssumptionSet> RequireAssumptionSetAsync(Guid id, CancellationToken ct) =>
        await _assumptionSets.GetAsync(_user.FirmId, id, ct) ?? throw new NotFoundException("AssumptionSet", id);

    private static void EnsureEditable(AnalysisBase a)
    {
        if (a.IsLocked)
        {
            throw new ConflictException("This analysis is locked because a report has been issued from it; create a new analysis to change it.");
        }
    }

    private async Task<T> LockAsync<T>(Guid id, string entityType, CancellationToken ct) where T : AnalysisBase
    {
        T a = await RequireAsync<T>(id, ct);
        if (a.Status != AnalysisStatus.Calculated)
        {
            throw new ConflictException("Only a calculated analysis can be locked.");
        }

        a.Lock(_clock.UtcNow);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, entityType, a.Id, "Locked", new { a.Id, a.Version, a.ResultHash }, ct);
        await _uow.SaveChangesAsync(ct);
        return a;
    }

    private async Task DeleteAsync<T>(Guid id, string entityType, CancellationToken ct) where T : AnalysisBase
    {
        T a = await RequireAsync<T>(id, ct);
        if (a.Status != AnalysisStatus.Draft)
        {
            throw new ConflictException("Only draft analyses can be deleted; calculated and locked analyses are retained for the audit trail.");
        }

        await _analyses.RemoveAsync(a, ct);
        await _audit.AppendAsync(_user.FirmId, _user.UserId, entityType, a.Id, "Deleted", new { a.Id }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static AssumptionOverrides? ParseOverrides(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonDefaults.Deserialize<AssumptionOverrides>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
