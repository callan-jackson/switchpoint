using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Calculation.StatePension;
using SwitchPoint.Calculation.Tax;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Market;

namespace SwitchPoint.Calculation.Cashflow;

/// <summary>A person in the household plan.</summary>
public sealed record PersonInput(string Name, DateOnly DateOfBirth, Sex Sex, TaxRegime TaxRegime, int RetirementAge, decimal? StatePensionForecastWeekly, int? StatePensionQualifyingYears, bool MpaaTriggered = false);

/// <summary>An income with its owner (0 = primary person, 1 = partner).</summary>
public sealed record CashflowIncome(PlanIncome Income, int PersonIndex = 0);

/// <summary>An asset with its owner and, for stochastic runs, its asset allocation.</summary>
public sealed record CashflowAsset(PlanAsset Asset, int PersonIndex = 0, AssetAllocation? Allocation = null);

/// <summary>Inputs to the cashflow engine. Amounts are in today's money at <see cref="StartDate"/>. See docs/methodology/cashflow.md.</summary>
public sealed record CashflowRequest
{
    public required DateOnly StartDate { get; init; }
    public required PersonInput Person { get; init; }
    public PersonInput? Partner { get; init; }
    public int PlanEndAge { get; init; } = 100;
    public IReadOnlyList<CashflowIncome> Incomes { get; init; } = [];
    public IReadOnlyList<PlanExpensePhase> Expenses { get; init; } = [];
    public IReadOnlyList<CashflowAsset> Assets { get; init; } = [];
    public IReadOnlyList<PlanEvent> Events { get; init; } = [];
    public PlanStrategy Strategy { get; init; } = PlanStrategy.Default;
    public required TaxYearParameters Tax { get; init; }
    public decimal Inflation { get; init; } = 0.02m;
    public decimal EarningsGrowth { get; init; } = 0.035m;
    public decimal StatePensionIncrease { get; init; } = 0.035m;
    public decimal IsaAllowance { get; init; } = 20_000m;

    /// <summary>Stochastic hook: nominal return for (plan year, asset) — null uses the asset's own growth rate.</summary>
    public Func<int, CashflowAsset, decimal>? ReturnOverride { get; init; }

    /// <summary>Stochastic hook: inflation for a plan year — null uses <see cref="Inflation"/>.</summary>
    public Func<int, decimal>? InflationOverride { get; init; }
}

public sealed record AssetValue(string Name, PlanAssetKind Kind, decimal Value, decimal ValueReal);

/// <summary>One plan year.</summary>
public sealed record CashflowRow(
    int Year,
    int Age,
    int? PartnerAge,
    decimal EmploymentIncome,
    decimal StatePensionIncome,
    decimal DbPensionIncome,
    decimal OtherIncome,
    decimal PensionWithdrawalsTaxable,
    decimal TaxFreeCash,
    decimal IsaWithdrawals,
    decimal GiaWithdrawals,
    decimal CashWithdrawals,
    decimal IncomeTax,
    decimal NationalInsurance,
    decimal CapitalGainsTax,
    decimal NetIncome,
    decimal NetIncomeReal,
    decimal Expenses,
    decimal Surplus,
    decimal Shortfall,
    decimal Contributions,
    IReadOnlyList<AssetValue> Assets,
    decimal TotalAssets,
    decimal TotalAssetsReal,
    IReadOnlyList<string> Warnings);

public sealed record CashflowResult(
    IReadOnlyList<CashflowRow> Rows,
    int? FirstShortfallAge,
    decimal TotalIncomeTax,
    decimal TotalShortfall,
    decimal LegacyAtEnd,
    decimal LegacyAtEndReal,
    decimal LumpSumAllowanceUsed)
{
    public bool Succeeds => FirstShortfallAge is null;
}

/// <summary>
/// Annual deterministic household cashflow from the start date to the plan end age with UK tax, pension
/// allowances, State Pension timing and a configurable withdrawal strategy. Pure and deterministic.
/// </summary>
public sealed class CashflowEngine
{
    private readonly ProjectionEngine _projection = new();

    public CashflowResult Run(CashflowRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Validate(r);

        UkTaxCalculator tax = new(r.Tax);
        StatePensionCalculator statePension = new(r.Tax);
        PensionAllowanceCalculator allowances = new(r.Tax);
        PersonInput[] people = r.Partner is null ? [r.Person] : [r.Person, r.Partner];
        StatePensionAge[] spa = [.. people.Select(p => StatePensionCalculator.StatePensionAgeFor(p.DateOfBirth))];
        decimal[] statePensionAnnual = [.. people.Select(p => statePension.AnnualEntitlement(p.StatePensionForecastWeekly, p.StatePensionQualifyingYears))];

        // Mutable asset state.
        List<AssetState> assets = [.. r.Assets.Select(a => new AssetState(a))];
        int startAge = r.Person.DateOfBirth.AgeOn(r.StartDate);
        int years = Math.Max(1, r.PlanEndAge - startAge);
        decimal lsaRemaining = r.Tax.Pensions.LumpSumAllowance;
        decimal lsaUsed = 0m;
        List<CashflowRow> rows = [];
        int? firstShortfall = null;
        decimal totalTax = 0m;
        decimal totalShortfall = 0m;
        decimal cumulativeInflation = 1m;

        for (int y = 0; y < years; y++)
        {
            int age = startAge + y;
            DateOnly yearStart = r.StartDate.AddYears(y);
            decimal inflation = r.InflationOverride?.Invoke(y) ?? r.Inflation;
            if (y > 0)
            {
                cumulativeInflation *= 1m + inflation;
            }

            List<string> warnings = [];
            int[] ages = [.. people.Select(p => p.DateOfBirth.AgeOn(yearStart))];

            // --- Guaranteed and earned income per person ---
            decimal[] employment = new decimal[people.Length];
            decimal[] pensionIncome = new decimal[people.Length];
            decimal[] otherIncome = new decimal[people.Length];
            decimal statePensionTotal = 0m;
            decimal dbTotal = 0m;
            foreach (CashflowIncome ci in r.Incomes)
            {
                int owner = Math.Min(ci.PersonIndex, people.Length - 1);
                PlanIncome inc = ci.Income;
                int ownerAge = ages[owner];
                if (ownerAge < inc.FromAge || (inc.ToAge is { } to && ownerAge > to))
                {
                    continue;
                }

                if (inc.Kind == IncomeKind.Employment && ownerAge >= people[owner].RetirementAge)
                {
                    continue;
                }

                decimal amount = inc.AnnualAmount * DecimalMath.IntegerPow(1m + inc.GrowthRate, y);
                switch (inc.Kind)
                {
                    case IncomeKind.Employment:
                    case IncomeKind.SelfEmployment:
                        employment[owner] += amount;
                        break;
                    case IncomeKind.DefinedBenefitPension:
                    case IncomeKind.Annuity:
                        pensionIncome[owner] += amount;
                        dbTotal += amount;
                        break;
                    case IncomeKind.StatePension:
                        pensionIncome[owner] += amount;
                        statePensionTotal += amount;
                        break;
                    default:
                        if (inc.IsTaxable)
                        {
                            otherIncome[owner] += amount;
                        }
                        else
                        {
                            otherIncome[owner] += 0m; // non-taxable other income handled as net below
                        }

                        break;
                }
            }

            decimal nonTaxableOther = r.Incomes.Where(ci => !ci.Income.IsTaxable && ci.Income.Kind == IncomeKind.Other)
                .Where(ci => ages[Math.Min(ci.PersonIndex, people.Length - 1)] >= ci.Income.FromAge && (ci.Income.ToAge is null || ages[Math.Min(ci.PersonIndex, people.Length - 1)] <= ci.Income.ToAge))
                .Sum(ci => ci.Income.AnnualAmount * DecimalMath.IntegerPow(1m + ci.Income.GrowthRate, y));

            // State Pension from SPA (pro rata in the SPA year), uprated from today.
            for (int p = 0; p < people.Length; p++)
            {
                if (statePensionAnnual[p] <= 0m)
                {
                    continue;
                }

                DateOnly yearEnd = yearStart.AddYears(1);
                if (spa[p].ReachedOn < yearEnd)
                {
                    decimal fraction = spa[p].ReachedOn <= yearStart ? 1m : (yearEnd.DayNumber - spa[p].ReachedOn.DayNumber) / 365.25m;
                    decimal sp = statePensionAnnual[p] * DecimalMath.IntegerPow(1m + r.StatePensionIncrease, y) * fraction;
                    pensionIncome[p] += sp;
                    statePensionTotal += sp;
                }
            }

            // --- Contributions (pre-retirement), with allowance checks ---
            decimal contributions = 0m;
            decimal[] grossPensionContributions = new decimal[people.Length];
            for (int p = 0; p < people.Length; p++)
            {
                decimal memberInput = 0m;
                decimal employerInput = 0m;
                foreach (AssetState a in assets.Where(a => Math.Min(a.Source.PersonIndex, people.Length - 1) == p))
                {
                    if (ages[p] >= people[p].RetirementAge)
                    {
                        continue;
                    }

                    decimal member = a.Source.Asset.AnnualContribution * DecimalMath.IntegerPow(1m + r.EarningsGrowth, y);
                    decimal employer = a.Source.Asset.EmployerContribution * DecimalMath.IntegerPow(1m + r.EarningsGrowth, y);
                    if (member + employer <= 0m)
                    {
                        continue;
                    }

                    a.Value += member + employer;
                    contributions += member + employer;
                    if (a.Kind is PlanAssetKind.UncrystallisedPension or PlanAssetKind.Drawdown)
                    {
                        memberInput += member;
                        employerInput += employer;
                    }
                }

                if (memberInput + employerInput > 0m)
                {
                    grossPensionContributions[p] = memberInput;
                    decimal threshold = employment[p] + pensionIncome[p] + otherIncome[p] - memberInput;
                    AllowanceCheckResult check = allowances.Check(new AllowanceCheckRequest(threshold, threshold + memberInput + employerInput, memberInput + employerInput, memberInput + employerInput, people[p].MpaaTriggered, []));
                    warnings.AddRange(check.Warnings.Select(w => $"{people[p].Name}: {w}"));
                    decimal cap = allowances.MaxRelievableContribution(employment[p]);
                    if (memberInput > cap)
                    {
                        warnings.Add($"{people[p].Name}: member contributions £{memberInput:N0} exceed the relievable maximum £{cap:N0} (greater of £3,600 and relevant UK earnings).");
                    }
                }
            }

            // --- Growth for the year (net of charges) ---
            foreach (AssetState a in assets)
            {
                if (a.Value <= 0m)
                {
                    continue;
                }

                decimal growth = r.ReturnOverride?.Invoke(y, a.Source) ?? a.Source.Asset.GrowthRate;
                if (a.Kind == PlanAssetKind.Property)
                {
                    a.Value *= 1m + growth;
                    continue;
                }

                ProjectionResult pr = _projection.Project(new ProjectionRequest
                {
                    StartValue = a.Value,
                    Months = 12,
                    GrowthRate = Math.Max(-0.99m, growth),
                    Charges = a.Source.Asset.Charges,
                    WeightedOcf = a.Source.Asset.Charges.FundCharge.Kind == FundChargeBasisKind.FromHoldings ? 0m : null,
                    Inflation = inflation,
                    ChargeInflation = r.Inflation,
                    InDrawdown = a.Kind == PlanAssetKind.Drawdown,
                    ApplyInitialAdviserCharge = false,
                });
                a.Value = pr.FinalValue;
                if (a.Kind == PlanAssetKind.GeneralInvestmentAccount)
                {
                    a.CostBasis = Math.Min(a.CostBasis, a.Value);
                }
            }

            // --- Crystallise up front at retirement if the strategy says so ---
            if (r.Strategy.Crystallisation == CrystallisationChoice.PclsUpFront)
            {
                for (int p = 0; p < people.Length; p++)
                {
                    if (ages[p] != people[p].RetirementAge || ages[p] < r.Tax.NormalMinimumPensionAgeOn(yearStart))
                    {
                        continue;
                    }

                    foreach (AssetState a in assets.Where(a => a.Kind == PlanAssetKind.UncrystallisedPension && Math.Min(a.Source.PersonIndex, people.Length - 1) == p && a.Value > 0m))
                    {
                        decimal tfc = Math.Min(a.Value * r.Tax.Pensions.TaxFreeCashFraction, lsaRemaining);
                        lsaRemaining -= tfc;
                        lsaUsed += tfc;
                        a.Value -= tfc;
                        a.Kind = PlanAssetKind.Drawdown;
                        AssetState cash = GetOrCreateCash(assets, p);
                        cash.Value += tfc;
                        warnings.Add($"{people[p].Name}: £{tfc:N0} tax-free cash taken from {a.Source.Asset.Name}; remaining lump sum allowance £{lsaRemaining:N0}.");
                    }
                }
            }

            // --- Expenses and events ---
            decimal expenses = r.Expenses.Where(e => age >= e.FromAge && (e.ToAge is null || age <= e.ToAge)).Sum(e => e.AnnualAmount) * cumulativeInflation;
            decimal events = r.Events.Where(e => e.AtAge == age).Sum(e => e.Amount) * cumulativeInflation;
            decimal need = expenses - events; // negative events are outflows (increase need), positive inflows reduce it

            // --- Tax on guaranteed income, then draw the gap ---
            decimal[] taxable = new decimal[people.Length];
            decimal incomeTaxTotal = 0m;
            decimal niTotal = 0m;
            decimal netGuaranteed = nonTaxableOther;
            for (int p = 0; p < people.Length; p++)
            {
                bool underSpa = spa[p].ReachedOn > yearStart;
                TaxComputation tc = tax.Compute(new TaxableIncome(employment[p], pensionIncome[p], otherIncome[p], GrossPensionContributions: grossPensionContributions[p], ReliefAtSourceContributions: grossPensionContributions[p], SubjectToNationalInsurance: underSpa), people[p].TaxRegime);
                taxable[p] = employment[p] + pensionIncome[p] + otherIncome[p];
                incomeTaxTotal += tc.IncomeTax;
                niTotal += tc.NationalInsurance;
                netGuaranteed += taxable[p] - tc.TotalDeductions - grossPensionContributions[p];
            }

            decimal gap = need - netGuaranteed;
            decimal pensionTaxable = 0m;
            decimal taxFreeCash = 0m;
            decimal isaOut = 0m;
            decimal giaOut = 0m;
            decimal cashOut = 0m;
            decimal cgt = 0m;
            decimal drawTax = 0m;

            if (gap > 0m)
            {
                foreach (PlanAssetKind kind in r.Strategy.WithdrawalOrder)
                {
                    if (gap <= 0.005m)
                    {
                        break;
                    }

                    foreach (AssetState a in assets.Where(a => a.Kind == kind && a.Value > 0m).OrderByDescending(a => a.Value))
                    {
                        if (gap <= 0.005m)
                        {
                            break;
                        }

                        int owner = Math.Min(a.Source.PersonIndex, people.Length - 1);
                        switch (kind)
                        {
                            case PlanAssetKind.Cash:
                            case PlanAssetKind.Isa:
                            {
                                decimal take = Math.Min(gap, a.Value);
                                a.Value -= take;
                                gap -= take;
                                if (kind == PlanAssetKind.Cash)
                                {
                                    cashOut += take;
                                }
                                else
                                {
                                    isaOut += take;
                                }

                                break;
                            }

                            case PlanAssetKind.GeneralInvestmentAccount:
                            {
                                // Gain fraction on a pooled basis; CGT at the rate for the owner's band after the annual exempt amount.
                                decimal gainFraction = a.Value <= 0m ? 0m : Math.Max(0m, 1m - (a.CostBasis / a.Value));
                                decimal rate = taxable[owner] > r.Tax.PersonalAllowance + r.Tax.BasicRateLimit ? r.Tax.CapitalGainsHigherRate : r.Tax.CapitalGainsBasicRate;
                                // Solve gross withdrawal W so that W − CGT = gap: CGT = max(0, W·g − AEA)·rate.
                                decimal w = gap;
                                for (int i = 0; i < 6; i++)
                                {
                                    decimal gains = w * gainFraction;
                                    decimal t = Math.Max(0m, gains - r.Tax.CapitalGainsAnnualExemptAmount) * rate;
                                    w = gap + t;
                                }

                                decimal take = Math.Min(w, a.Value);
                                decimal tookGains = take * gainFraction;
                                decimal cgtOnTake = Math.Max(0m, tookGains - r.Tax.CapitalGainsAnnualExemptAmount) * rate;
                                a.CostBasis -= take * (1m - gainFraction);
                                a.Value -= take;
                                cgt += cgtOnTake;
                                giaOut += take;
                                gap -= take - cgtOnTake;
                                break;
                            }

                            case PlanAssetKind.Drawdown:
                            case PlanAssetKind.UncrystallisedPension:
                            {
                                if (ages[owner] < r.Tax.NormalMinimumPensionAgeOn(yearStart))
                                {
                                    continue;
                                }

                                bool uncrystallised = kind == PlanAssetKind.UncrystallisedPension;
                                decimal tfcFraction = uncrystallised ? r.Tax.Pensions.TaxFreeCashFraction : 0m;
                                // Net from a gross withdrawal W: W·tfc + (W·(1−tfc) − tax on taxable part). Solve for W by root-finding on the tax function.
                                decimal other = taxable[owner];
                                decimal Net(decimal w)
                                {
                                    decimal tfc = Math.Min(w * tfcFraction, lsaRemaining);
                                    decimal taxablePart = w - tfc;
                                    decimal t = TaxOnExtra(tax, other, taxablePart, people[owner].TaxRegime);
                                    return tfc + taxablePart - t;
                                }

                                decimal maxNet = Net(a.Value);
                                decimal gross;
                                if (maxNet <= gap)
                                {
                                    gross = a.Value;
                                }
                                else
                                {
                                    gross = RootFinder.Solve(w => Net(w) - gap, 0m, a.Value, RootOptions.Default).Value;
                                }

                                decimal tfcTaken = Math.Min(gross * tfcFraction, lsaRemaining);
                                lsaRemaining -= tfcTaken;
                                lsaUsed += tfcTaken;
                                decimal taxableTaken = gross - tfcTaken;
                                decimal extraTax = TaxOnExtra(tax, other, taxableTaken, people[owner].TaxRegime);
                                taxable[owner] += taxableTaken;
                                pensionTaxable += taxableTaken;
                                taxFreeCash += tfcTaken;
                                drawTax += extraTax;
                                a.Value -= gross;
                                gap -= tfcTaken + taxableTaken - extraTax;
                                if (uncrystallised && r.Strategy.Crystallisation == CrystallisationChoice.PhasedDrawdown)
                                {
                                    // The taxable 75% of a phased crystallisation moves into drawdown; we model the withdrawal directly, so nothing more to do.
                                }

                                break;
                            }

                            case PlanAssetKind.Property:
                            case PlanAssetKind.OnshoreBond:
                            default:
                                continue;
                        }
                    }
                }
            }

            decimal shortfall = Math.Max(0m, gap);
            decimal surplus = Math.Max(0m, -gap);
            if (shortfall > 0.005m)
            {
                firstShortfall ??= age;
                totalShortfall += shortfall;
            }

            // --- Surplus reinvestment ---
            if (surplus > 0m)
            {
                if (r.Strategy.ReinvestSurplusIntoIsa)
                {
                    AssetState? isa = assets.FirstOrDefault(a => a.Kind == PlanAssetKind.Isa);
                    decimal toIsa = Math.Min(surplus, r.IsaAllowance * cumulativeInflation);
                    if (isa is null && toIsa > 0m)
                    {
                        isa = new AssetState(new CashflowAsset(new PlanAsset("ISA (surplus)", PlanAssetKind.Isa, 0m, (r.Assets.Count > 0 ? r.Assets[0].Asset.GrowthRate : 0.04m), ChargeSchedule.None)));
                        assets.Add(isa);
                    }

                    if (isa is not null)
                    {
                        isa.Value += toIsa;
                        surplus -= toIsa;
                    }
                }

                if (surplus > 0m)
                {
                    AssetState cash = GetOrCreateCash(assets, 0);
                    cash.Value += surplus;
                }
            }

            incomeTaxTotal += drawTax;
            totalTax += incomeTaxTotal;
            decimal totalAssets = assets.Sum(a => a.Value);
            decimal netIncome = taxable.Sum() + nonTaxableOther + taxFreeCash + isaOut + giaOut + cashOut - incomeTaxTotal - niTotal - cgt - grossPensionContributions.Sum();
            rows.Add(new CashflowRow(
                y + 1, age, people.Length > 1 ? ages[1] : null,
                employment.Sum(), statePensionTotal, dbTotal, otherIncome.Sum() + nonTaxableOther,
                pensionTaxable, taxFreeCash, isaOut, giaOut, cashOut,
                incomeTaxTotal, niTotal, cgt, netIncome, netIncome / cumulativeInflation, need, Math.Max(0m, -gap), shortfall, contributions,
                [.. assets.Select(a => new AssetValue(a.Source.Asset.Name, a.Kind, a.Value, a.Value / cumulativeInflation))],
                totalAssets, totalAssets / cumulativeInflation, warnings));
        }

        CashflowRow last = rows[^1];
        return new CashflowResult(rows, firstShortfall, totalTax, totalShortfall, last.TotalAssets, last.TotalAssetsReal, lsaUsed);
    }

    /// <summary>The level real annual spend (in today's money) the plan can support to the end age without a shortfall.</summary>
    public decimal SustainableSpend(CashflowRequest r, decimal upperBound = 500_000m)
    {
        ArgumentNullException.ThrowIfNull(r);
        int startAge = r.Person.DateOfBirth.AgeOn(r.StartDate);
        CashflowResult RunAt(decimal spend) => Run(r with { Expenses = [new PlanExpensePhase("Level spend", spend, startAge, null)] });
        if (RunAt(upperBound).Succeeds)
        {
            return upperBound;
        }

        decimal lo = 0m;
        decimal hi = upperBound;
        for (int i = 0; i < 40 && hi - lo > 1m; i++)
        {
            decimal mid = (lo + hi) / 2m;
            if (RunAt(mid).Succeeds)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return Math.Floor(lo);
    }

    private static decimal TaxOnExtra(UkTaxCalculator tax, decimal otherTaxable, decimal extra, TaxRegime regime)
    {
        if (extra <= 0m)
        {
            return 0m;
        }

        decimal with = tax.Compute(new TaxableIncome(PensionIncome: otherTaxable + extra, SubjectToNationalInsurance: false), regime).IncomeTax;
        decimal without = tax.Compute(new TaxableIncome(PensionIncome: otherTaxable, SubjectToNationalInsurance: false), regime).IncomeTax;
        return with - without;
    }

    private static AssetState GetOrCreateCash(List<AssetState> assets, int owner)
    {
        AssetState? cash = assets.FirstOrDefault(a => a.Kind == PlanAssetKind.Cash && Math.Min(a.Source.PersonIndex, 1) == owner) ?? assets.FirstOrDefault(a => a.Kind == PlanAssetKind.Cash);
        if (cash is null)
        {
            cash = new AssetState(new CashflowAsset(new PlanAsset("Cash", PlanAssetKind.Cash, 0m, 0.02m, ChargeSchedule.None), owner));
            assets.Add(cash);
        }

        return cash;
    }

    private static void Validate(CashflowRequest r)
    {
        ArgumentNullException.ThrowIfNull(r.Person);
        ArgumentNullException.ThrowIfNull(r.Tax);
        if (r.PlanEndAge is < 55 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(r), r.PlanEndAge, "Plan end age must be between 55 and 120.");
        }

        if (r.Inflation <= -1m || r.EarningsGrowth <= -1m)
        {
            throw new ArgumentOutOfRangeException(nameof(r), "Inflation and earnings growth must exceed -100%.");
        }

        r.Strategy.Validate();
        foreach (CashflowAsset a in r.Assets)
        {
            a.Asset.Validate();
        }

        foreach (CashflowIncome i in r.Incomes)
        {
            i.Income.Validate();
        }

        foreach (PlanExpensePhase e in r.Expenses)
        {
            e.Validate();
        }
    }

    private sealed class AssetState(CashflowAsset source)
    {
        public CashflowAsset Source { get; } = source;
        public PlanAssetKind Kind { get; set; } = source.Asset.Kind;
        public decimal Value { get; set; } = source.Asset.Value;
        public decimal CostBasis { get; set; } = source.Asset.Kind == PlanAssetKind.GeneralInvestmentAccount ? (source.Asset.CostBasis > 0m ? source.Asset.CostBasis : source.Asset.Value) : 0m;
    }
}

internal static class DateOnlyExtensions
{
    public static int AgeOn(this DateOnly dateOfBirth, DateOnly date)
    {
        int age = date.Year - dateOfBirth.Year;
        if (date < dateOfBirth.AddYears(age))
        {
            age--;
        }

        return age;
    }
}
