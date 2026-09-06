namespace SwitchPoint.Calculation.Tax;

/// <summary>One income tax band: the width of taxable income it covers (null = unbounded) and its rate.</summary>
public sealed record TaxBand(string Name, decimal? Width, decimal Rate);

/// <summary>National Insurance class 1 (employee) parameters.</summary>
public sealed record NationalInsuranceParameters(decimal PrimaryThreshold, decimal UpperEarningsLimit, decimal MainRate, decimal AdditionalRate);

/// <summary>Pension allowance parameters (Finance Act 2004 as amended).</summary>
public sealed record PensionAllowanceParameters(
    decimal AnnualAllowance,
    decimal MoneyPurchaseAnnualAllowance,
    decimal TaperThresholdIncome,
    decimal TaperAdjustedIncome,
    decimal TaperReductionRate,
    decimal TaperMinimum,
    int CarryForwardYears,
    decimal BasicAmountRelievable,
    decimal LumpSumAllowance,
    decimal LumpSumAndDeathBenefitAllowance,
    decimal TaxFreeCashFraction,
    decimal ReliefAtSourceRate);

/// <summary>
/// All rates and thresholds for one tax year. Values for 2026/27 are in <see cref="TaxYears"/>, transcribed from
/// data/tax-years/2026-27.json (verified 6 September 2026). Every engine reads from this record; nothing is hard-coded elsewhere.
/// </summary>
public sealed record TaxYearParameters(
    string Name,
    DateOnly StartsOn,
    decimal PersonalAllowance,
    decimal PersonalAllowanceTaperThreshold,
    decimal PersonalAllowanceTaperRate,
    IReadOnlyList<TaxBand> RestOfUkBands,
    IReadOnlyList<TaxBand> ScottishBands,
    decimal SavingsStartingRateBand,
    decimal PersonalSavingsAllowanceBasic,
    decimal PersonalSavingsAllowanceHigher,
    IReadOnlyList<TaxBand> SavingsBands,
    decimal DividendAllowance,
    IReadOnlyList<TaxBand> DividendBands,
    decimal CapitalGainsAnnualExemptAmount,
    decimal CapitalGainsBasicRate,
    decimal CapitalGainsHigherRate,
    NationalInsuranceParameters NationalInsurance,
    PensionAllowanceParameters Pensions,
    decimal FullNewStatePensionWeekly,
    int StatePensionQualifyingYearsForFull,
    int StatePensionMinimumQualifyingYears,
    int NormalMinimumPensionAge,
    DateOnly NormalMinimumPensionAgeRisesOn,
    int NormalMinimumPensionAgeAfterRise)
{
    /// <summary>Basic-rate limit (top of the basic band above the personal allowance) for rest-of-UK.</summary>
    public decimal BasicRateLimit => RestOfUkBands[0].Width!.Value;

    /// <summary>Normal minimum pension age applying on <paramref name="date"/>.</summary>
    public int NormalMinimumPensionAgeOn(DateOnly date) => date >= NormalMinimumPensionAgeRisesOn ? NormalMinimumPensionAgeAfterRise : NormalMinimumPensionAge;
}

/// <summary>Registry of known tax years.</summary>
public static class TaxYears
{
    /// <summary>Tax year 6 April 2026 – 5 April 2027. Sources: gov.uk/income-tax-rates, gov.scot 2026-27 bands, HMRC PTM, DWP benefit rates 2026/27.</summary>
    public static TaxYearParameters Y2026_27 { get; } = new(
        Name: "2026/27",
        StartsOn: new DateOnly(2026, 4, 6),
        PersonalAllowance: 12_570m,
        PersonalAllowanceTaperThreshold: 100_000m,
        PersonalAllowanceTaperRate: 0.5m,
        RestOfUkBands:
        [
            new TaxBand("Basic", 37_700m, 0.20m),
            new TaxBand("Higher", 87_440m, 0.40m),   // 37,700 + 87,440 = 125,140 taxable above the PA
            new TaxBand("Additional", null, 0.45m),
        ],
        ScottishBands:
        [
            new TaxBand("Starter", 3_967m, 0.19m),        // to £16,537 gross
            new TaxBand("Basic", 12_989m, 0.20m),         // to £29,526
            new TaxBand("Intermediate", 14_136m, 0.21m),  // to £43,662
            new TaxBand("Higher", 31_338m, 0.42m),        // to £75,000
            new TaxBand("Advanced", 50_140m, 0.45m),      // to £125,140
            new TaxBand("Top", null, 0.48m),
        ],
        SavingsStartingRateBand: 5_000m,
        PersonalSavingsAllowanceBasic: 1_000m,
        PersonalSavingsAllowanceHigher: 500m,
        SavingsBands:
        [
            new TaxBand("Basic", 37_700m, 0.20m),
            new TaxBand("Higher", 87_440m, 0.40m),
            new TaxBand("Additional", null, 0.45m),
        ],
        DividendAllowance: 500m,
        DividendBands:
        [
            new TaxBand("Ordinary", 37_700m, 0.1075m),
            new TaxBand("Upper", 87_440m, 0.3575m),
            new TaxBand("Additional", null, 0.3935m),
        ],
        CapitalGainsAnnualExemptAmount: 3_000m,
        CapitalGainsBasicRate: 0.18m,
        CapitalGainsHigherRate: 0.24m,
        NationalInsurance: new NationalInsuranceParameters(12_570m, 50_270m, 0.08m, 0.02m),
        Pensions: new PensionAllowanceParameters(
            AnnualAllowance: 60_000m,
            MoneyPurchaseAnnualAllowance: 10_000m,
            TaperThresholdIncome: 200_000m,
            TaperAdjustedIncome: 260_000m,
            TaperReductionRate: 0.5m,
            TaperMinimum: 10_000m,
            CarryForwardYears: 3,
            BasicAmountRelievable: 3_600m,
            LumpSumAllowance: 268_275m,
            LumpSumAndDeathBenefitAllowance: 1_073_100m,
            TaxFreeCashFraction: 0.25m,
            ReliefAtSourceRate: 0.20m),
        FullNewStatePensionWeekly: 241.30m,
        StatePensionQualifyingYearsForFull: 35,
        StatePensionMinimumQualifyingYears: 10,
        NormalMinimumPensionAge: 55,
        NormalMinimumPensionAgeRisesOn: new DateOnly(2028, 4, 6),
        NormalMinimumPensionAgeAfterRise: 57);

    /// <summary>Tax year 2027/28 as announced (savings rates 22/42/47%); other values carried forward under the freeze to 2031.</summary>
    public static TaxYearParameters Y2027_28 { get; } = Y2026_27 with
    {
        Name = "2027/28",
        StartsOn = new DateOnly(2027, 4, 6),
        SavingsBands =
        [
            new TaxBand("Basic", 37_700m, 0.22m),
            new TaxBand("Higher", 87_440m, 0.42m),
            new TaxBand("Additional", null, 0.47m),
        ],
    };

    private static readonly IReadOnlyDictionary<string, TaxYearParameters> ByName = new Dictionary<string, TaxYearParameters>(StringComparer.Ordinal)
    {
        [Y2026_27.Name] = Y2026_27,
        [Y2027_28.Name] = Y2027_28,
    };

    public static TaxYearParameters Get(string name) =>
        ByName.TryGetValue(name, out TaxYearParameters? p) ? p : throw new KeyNotFoundException($"Tax year '{name}' is not in the registry.");

    /// <summary>The tax year containing <paramref name="date"/>; beyond the last known year the latest parameters are used (frozen thresholds).</summary>
    public static TaxYearParameters For(DateOnly date)
    {
        TaxYearParameters latest = Y2026_27;
        foreach (TaxYearParameters p in ByName.Values.OrderBy(p => p.StartsOn))
        {
            if (date >= p.StartsOn)
            {
                latest = p;
            }
        }

        return latest;
    }

    public static IEnumerable<TaxYearParameters> All => ByName.Values.OrderBy(p => p.StartsOn);
}
