using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Analysis;

public enum IncomeKind { Employment = 0, SelfEmployment = 1, Rental = 2, DefinedBenefitPension = 3, Annuity = 4, StatePension = 5, Other = 6 }

public enum PlanAssetKind { UncrystallisedPension = 0, Drawdown = 1, Isa = 2, GeneralInvestmentAccount = 3, Cash = 4, Property = 5, OnshoreBond = 6 }

public enum WithdrawalRule { GapFill = 0, FixedAmount = 1, PercentOfPot = 2 }

public enum CrystallisationChoice { PclsUpFront = 0, PhasedUfpls = 1, PhasedDrawdown = 2 }

/// <summary>An income stream in today's money, from one age to another (inclusive), growing at a rate.</summary>
public sealed record PlanIncome(string Name, IncomeKind Kind, decimal AnnualAmount, int FromAge, int? ToAge, decimal GrowthRate, bool IsTaxable = true)
{
    public PlanIncome Validate()
    {
        Guard.NotNullOrWhiteSpace(Name);
        Guard.NonNegative(AnnualAmount);
        Guard.InRange(FromAge, 16, 110, nameof(FromAge));
        Guard.Against(ToAge is { } t && t < FromAge, "An income cannot end before it starts.");
        Guard.InRange(GrowthRate, -0.1m, 0.2m, nameof(GrowthRate));
        return this;
    }
}

/// <summary>Spending in today's money over an age range, inflated by CPI.</summary>
public sealed record PlanExpensePhase(string Name, decimal AnnualAmount, int FromAge, int? ToAge)
{
    public PlanExpensePhase Validate()
    {
        Guard.NotNullOrWhiteSpace(Name);
        Guard.NonNegative(AnnualAmount);
        Guard.InRange(FromAge, 16, 110, nameof(FromAge));
        Guard.Against(ToAge is { } t && t < FromAge, "An expense phase cannot end before it starts.");
        return this;
    }
}

/// <summary>An asset in the plan, optionally linked to a scheme record, with its own growth and charges.</summary>
public sealed record PlanAsset(string Name, PlanAssetKind Kind, decimal Value, decimal GrowthRate, ChargeSchedule Charges, Guid? SchemeId = null, decimal CostBasis = 0m, decimal AnnualContribution = 0m, decimal EmployerContribution = 0m, bool SalarySacrifice = false)
{
    public PlanAsset Validate()
    {
        Guard.NotNullOrWhiteSpace(Name);
        Guard.NonNegative(Value);
        Guard.InRange(GrowthRate, -0.1m, 0.2m, nameof(GrowthRate));
        Guard.NotNull(Charges);
        Guard.NonNegative(CostBasis);
        Guard.NonNegative(AnnualContribution);
        Guard.NonNegative(EmployerContribution);
        return this;
    }
}

/// <summary>A one-off inflow (positive) or outflow (negative) at a given age, in today's money.</summary>
public sealed record PlanEvent(string Name, int AtAge, decimal Amount)
{
    public PlanEvent Validate()
    {
        Guard.NotNullOrWhiteSpace(Name);
        Guard.InRange(AtAge, 16, 110, nameof(AtAge));
        return this;
    }
}

/// <summary>How the plan draws on assets and crystallises pensions.</summary>
public sealed record PlanStrategy(
    IReadOnlyList<PlanAssetKind> WithdrawalOrder,
    CrystallisationChoice Crystallisation,
    WithdrawalRule DrawdownRule,
    decimal DrawdownParameter,
    bool ReinvestSurplusIntoIsa,
    int? AnnuityPurchaseAge)
{
    public static PlanStrategy Default { get; } = new(
        [PlanAssetKind.Cash, PlanAssetKind.GeneralInvestmentAccount, PlanAssetKind.Isa, PlanAssetKind.Drawdown, PlanAssetKind.UncrystallisedPension],
        CrystallisationChoice.PhasedDrawdown,
        WithdrawalRule.GapFill,
        0m,
        true,
        null);

    public PlanStrategy Validate()
    {
        Guard.NotEmpty(WithdrawalOrder);
        Guard.Against(WithdrawalOrder.Distinct().Count() != WithdrawalOrder.Count, "Withdrawal order must not repeat an asset kind.");
        Guard.NonNegative(DrawdownParameter);
        Guard.Against(AnnuityPurchaseAge is { } a && (a < 55 || a > 90), "Annuity purchase age must be between 55 and 90.");
        return this;
    }
}

/// <summary>A household cashflow plan: people, incomes, expenses, assets, strategy and events.</summary>
public sealed class CashflowPlan : AnalysisBase
{
    private readonly List<PlanIncome> _incomes = [];
    private readonly List<PlanExpensePhase> _expenses = [];
    private readonly List<PlanAsset> _assets = [];
    private readonly List<PlanEvent> _events = [];

    public CashflowPlan(Guid id, Guid firmId, Guid clientId, Guid assumptionSetId, Guid createdBy, string title, DateTime createdAtUtc, Guid? partnerClientId = null, int planEndAge = 100)
        : base(id, firmId, clientId, assumptionSetId, createdBy, createdAtUtc)
    {
        Title = Guard.NotNullOrWhiteSpace(title);
        PartnerClientId = partnerClientId;
        PlanEndAge = Guard.InRange(planEndAge, 60, 110);
    }

    public string Title { get; private set; }
    public Guid? PartnerClientId { get; private set; }
    public int PlanEndAge { get; private set; }
    public IReadOnlyList<PlanIncome> Incomes => _incomes;
    public IReadOnlyList<PlanExpensePhase> Expenses => _expenses;
    public IReadOnlyList<PlanAsset> Assets => _assets;
    public IReadOnlyList<PlanEvent> Events => _events;
    public PlanStrategy Strategy { get; private set; } = PlanStrategy.Default;

    /// <summary>Seed used for the stochastic run so results are reproducible; null until first run.</summary>
    public ulong? StochasticSeed { get; private set; }

    public int StochasticPaths { get; private set; } = 1000;

    public void ReplaceIncomes(IEnumerable<PlanIncome> incomes, DateTime nowUtc)
    {
        _incomes.Clear();
        _incomes.AddRange(Guard.NotNull(incomes).Select(i => i.Validate()));
        Invalidate(nowUtc);
    }

    public void ReplaceExpenses(IEnumerable<PlanExpensePhase> expenses, DateTime nowUtc)
    {
        _expenses.Clear();
        _expenses.AddRange(Guard.NotNull(expenses).Select(e => e.Validate()));
        Invalidate(nowUtc);
    }

    public void ReplaceAssets(IEnumerable<PlanAsset> assets, DateTime nowUtc)
    {
        _assets.Clear();
        _assets.AddRange(Guard.NotNull(assets).Select(a => a.Validate()));
        Invalidate(nowUtc);
    }

    public void ReplaceEvents(IEnumerable<PlanEvent> events, DateTime nowUtc)
    {
        _events.Clear();
        _events.AddRange(Guard.NotNull(events).Select(e => e.Validate()));
        Invalidate(nowUtc);
    }

    public void SetStrategy(PlanStrategy strategy, DateTime nowUtc)
    {
        Strategy = Guard.NotNull(strategy).Validate();
        Invalidate(nowUtc);
    }

    public void SetHorizon(int planEndAge, Guid? partnerClientId, DateTime nowUtc)
    {
        PlanEndAge = Guard.InRange(planEndAge, 60, 110);
        PartnerClientId = partnerClientId;
        Invalidate(nowUtc);
    }

    public void SetStochasticSettings(ulong seed, int paths, DateTime nowUtc)
    {
        StochasticSeed = seed;
        StochasticPaths = Guard.InRange(paths, 100, 10_000);
        Invalidate(nowUtc);
    }

    public void Rename(string title, DateTime nowUtc)
    {
        EnsureEditable();
        Title = Guard.NotNullOrWhiteSpace(title);
        Touch(nowUtc);
    }
}
