using SwitchPoint.Domain.Clients;

namespace SwitchPoint.Calculation.Tax;

/// <summary>Income sources for one tax year, all gross annual amounts in £.</summary>
/// <param name="EarnedIncome">Employment and self-employment income (subject to NI when <paramref name="SubjectToNationalInsurance"/>).</param>
/// <param name="PensionIncome">DB pensions, annuities, State Pension, taxable drawdown/UFPLS (75%).</param>
/// <param name="OtherNonSavingsIncome">Rental and other non-savings income.</param>
/// <param name="SavingsInterest">Bank/bond interest outside ISAs.</param>
/// <param name="Dividends">Dividends outside ISAs.</param>
/// <param name="GrossPensionContributions">Relief-at-source and net-pay member contributions, gross, which reduce adjusted net income and extend the basic-rate band (RAS).</param>
/// <param name="ReliefAtSourceContributions">The part of <paramref name="GrossPensionContributions"/> paid via relief at source (extends the basic and higher rate limits).</param>
/// <param name="SubjectToNationalInsurance">False once over State Pension age.</param>
public sealed record TaxableIncome(
    decimal EarnedIncome = 0m,
    decimal PensionIncome = 0m,
    decimal OtherNonSavingsIncome = 0m,
    decimal SavingsInterest = 0m,
    decimal Dividends = 0m,
    decimal GrossPensionContributions = 0m,
    decimal ReliefAtSourceContributions = 0m,
    bool SubjectToNationalInsurance = true)
{
    public decimal NonSavings => EarnedIncome + PensionIncome + OtherNonSavingsIncome;
    public decimal Total => NonSavings + SavingsInterest + Dividends;
}

/// <summary>One line of the computation, for the report's working.</summary>
public sealed record TaxLine(string Category, string Band, decimal Amount, decimal Rate, decimal Tax);

/// <summary>Full breakdown of a year's income tax and NI.</summary>
public sealed record TaxComputation(
    string TaxYear,
    TaxRegime Regime,
    decimal AdjustedNetIncome,
    decimal PersonalAllowance,
    decimal TaxableIncome,
    IReadOnlyList<TaxLine> Lines,
    decimal IncomeTax,
    decimal NationalInsurance,
    decimal MarginalRate)
{
    public decimal TotalDeductions => IncomeTax + NationalInsurance;
}

/// <summary>
/// UK income tax (rest-of-UK and Scottish rates) and class 1 employee NI for one tax year.
/// Ordering follows ITA 2007 s16: non-savings, then savings, then dividends. See docs/methodology/tax.md.
/// </summary>
public sealed class UkTaxCalculator
{
    private readonly TaxYearParameters _p;

    public UkTaxCalculator(TaxYearParameters parameters)
    {
        _p = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public TaxYearParameters Parameters => _p;

    public TaxComputation Compute(TaxableIncome income, TaxRegime regime)
    {
        ArgumentNullException.ThrowIfNull(income);
        Validate(income);

        decimal adjustedNetIncome = Math.Max(0m, income.Total - income.GrossPensionContributions);
        decimal personalAllowance = PersonalAllowanceFor(adjustedNetIncome);

        // Relief at source extends the basic and higher rate limits by the gross contribution (ITA 2007 s192).
        decimal extension = income.ReliefAtSourceContributions;
        IReadOnlyList<TaxBand> nonSavingsBands = Extend(regime == TaxRegime.Scotland ? _p.ScottishBands : _p.RestOfUkBands, extension, regime);
        IReadOnlyList<TaxBand> savingsBands = Extend(_p.SavingsBands, extension, TaxRegime.RestOfUk);
        IReadOnlyList<TaxBand> dividendBands = Extend(_p.DividendBands, extension, TaxRegime.RestOfUk);

        List<TaxLine> lines = [];
        decimal allowanceLeft = personalAllowance;
        decimal bandPosition = 0m; // cumulative taxable income already placed in bands (shared across income types)

        // 1. Non-savings income.
        decimal nonSavings = income.NonSavings;
        decimal nonSavingsAfterPa = Math.Max(0m, nonSavings - allowanceLeft);
        allowanceLeft = Math.Max(0m, allowanceLeft - nonSavings);
        decimal marginal = 0m;
        Place(lines, "Non-savings", nonSavingsBands, nonSavingsAfterPa, ref bandPosition, ref marginal);

        // 2. Savings income: personal allowance remainder, then starting rate band (reduced £1 for £1 by non-savings taxable income),
        //    then personal savings allowance (0% but occupies band space), then the UK savings rates.
        decimal savings = income.SavingsInterest;
        decimal savingsAfterPa = Math.Max(0m, savings - allowanceLeft);
        allowanceLeft = Math.Max(0m, allowanceLeft - savings);
        if (savingsAfterPa > 0m)
        {
            decimal startingBand = Math.Max(0m, _p.SavingsStartingRateBand - nonSavingsAfterPa);
            decimal atStartingRate = Math.Min(savingsAfterPa, startingBand);
            if (atStartingRate > 0m)
            {
                lines.Add(new TaxLine("Savings", "Starting rate for savings", atStartingRate, 0m, 0m));
                bandPosition += atStartingRate;
                savingsAfterPa -= atStartingRate;
            }

            decimal psa = PersonalSavingsAllowanceFor(nonSavingsAfterPa + savingsAfterPa + atStartingRate + Math.Max(0m, income.Dividends - allowanceLeft), savingsBands);
            decimal atPsa = Math.Min(savingsAfterPa, psa);
            if (atPsa > 0m)
            {
                lines.Add(new TaxLine("Savings", "Personal savings allowance", atPsa, 0m, 0m));
                bandPosition += atPsa;
                savingsAfterPa -= atPsa;
            }

            Place(lines, "Savings", savingsBands, savingsAfterPa, ref bandPosition, ref marginal);
        }

        // 3. Dividends: allowance remainder, then dividend allowance (0% but occupies band space), then dividend rates.
        decimal dividends = income.Dividends;
        decimal dividendsAfterPa = Math.Max(0m, dividends - allowanceLeft);
        if (dividendsAfterPa > 0m)
        {
            decimal atAllowance = Math.Min(dividendsAfterPa, _p.DividendAllowance);
            lines.Add(new TaxLine("Dividends", "Dividend allowance", atAllowance, 0m, 0m));
            bandPosition += atAllowance;
            Place(lines, "Dividends", dividendBands, dividendsAfterPa - atAllowance, ref bandPosition, ref marginal);
        }

        decimal incomeTax = lines.Sum(l => l.Tax);
        decimal ni = income.SubjectToNationalInsurance ? NationalInsuranceFor(income.EarnedIncome) : 0m;
        decimal taxable = nonSavingsAfterPa + Math.Max(0m, savings - Math.Max(0m, personalAllowance - nonSavings)) + dividendsAfterPa;

        return new TaxComputation(_p.Name, regime, adjustedNetIncome, personalAllowance, taxable, lines, incomeTax, ni, marginal);
    }

    /// <summary>Personal allowance after the £1-per-£2 taper above the threshold.</summary>
    public decimal PersonalAllowanceFor(decimal adjustedNetIncome)
    {
        decimal excess = Math.Max(0m, adjustedNetIncome - _p.PersonalAllowanceTaperThreshold);
        return Math.Max(0m, _p.PersonalAllowance - (excess * _p.PersonalAllowanceTaperRate));
    }

    /// <summary>Class 1 employee NI on earned income.</summary>
    public decimal NationalInsuranceFor(decimal earnedIncome)
    {
        NationalInsuranceParameters ni = _p.NationalInsurance;
        decimal main = Math.Max(0m, Math.Min(earnedIncome, ni.UpperEarningsLimit) - ni.PrimaryThreshold) * ni.MainRate;
        decimal additional = Math.Max(0m, earnedIncome - ni.UpperEarningsLimit) * ni.AdditionalRate;
        return main + additional;
    }

    /// <summary>
    /// Tax on a single flexible pension payment taxed on the emergency month-1 basis: one twelfth of the
    /// personal allowance and of each band is applied to the taxable part of the payment.
    /// </summary>
    public decimal EmergencyMonthOneTax(decimal taxablePayment, TaxRegime regime)
    {
        if (taxablePayment <= 0m)
        {
            return 0m;
        }

        decimal remaining = Math.Max(0m, taxablePayment - (_p.PersonalAllowance / 12m));
        decimal tax = 0m;
        foreach (TaxBand band in regime == TaxRegime.Scotland ? _p.ScottishBands : _p.RestOfUkBands)
        {
            decimal width = band.Width is { } w ? w / 12m : decimal.MaxValue;
            decimal slice = Math.Min(remaining, width);
            tax += slice * band.Rate;
            remaining -= slice;
            if (remaining <= 0m)
            {
                break;
            }
        }

        return tax;
    }

    /// <summary>
    /// Gross income needed so that after income tax (non-savings only, no NI) the client keeps <paramref name="netRequired"/>.
    /// Solved by walking bands, so it is exact and fast. Other income already in the year shifts the starting position.
    /// </summary>
    public decimal GrossForNet(decimal netRequired, decimal otherNonSavingsIncome, TaxRegime regime)
    {
        if (netRequired <= 0m)
        {
            return 0m;
        }

        // Walk from the current gross position upward; each segment has a constant marginal rate
        // (including the 60% effective rate inside the PA taper).
        decimal gross = otherNonSavingsIncome;
        decimal netSoFar = 0m;
        int guard = 0;
        while (netSoFar < netRequired && guard++ < 50)
        {
            decimal rate = MarginalRateAt(gross, regime);
            decimal segmentEnd = NextBreakpointAbove(gross, regime);
            decimal netCapacity = (segmentEnd - gross) * (1m - rate);
            if (netSoFar + netCapacity >= netRequired || segmentEnd == decimal.MaxValue)
            {
                gross += (netRequired - netSoFar) / (1m - rate);
                netSoFar = netRequired;
            }
            else
            {
                gross = segmentEnd;
                netSoFar += netCapacity;
            }
        }

        return gross - otherNonSavingsIncome;
    }

    /// <summary>Effective marginal income tax rate on the next £1 of non-savings income at <paramref name="grossIncome"/>.</summary>
    public decimal MarginalRateAt(decimal grossIncome, TaxRegime regime)
    {
        IReadOnlyList<TaxBand> bands = regime == TaxRegime.Scotland ? _p.ScottishBands : _p.RestOfUkBands;
        decimal pa = PersonalAllowanceFor(grossIncome);
        if (grossIncome < pa)
        {
            return 0m; // still inside the personal allowance
        }

        bool inTaper = grossIncome >= _p.PersonalAllowanceTaperThreshold && pa > 0m;
        decimal taxable = Math.Max(0m, grossIncome - pa);
        decimal bandRate = RateForTaxable(bands, taxable);
        // Inside the taper every £2 of income removes £1 of allowance, taxed at the band rate: effective = rate × (1 + taper rate).
        return inTaper ? bandRate * (1m + _p.PersonalAllowanceTaperRate) : bandRate;
    }

    private decimal NextBreakpointAbove(decimal grossIncome, TaxRegime regime)
    {
        IReadOnlyList<TaxBand> bands = regime == TaxRegime.Scotland ? _p.ScottishBands : _p.RestOfUkBands;
        List<decimal> points = [_p.PersonalAllowance, _p.PersonalAllowanceTaperThreshold, _p.PersonalAllowanceTaperThreshold + (_p.PersonalAllowance / _p.PersonalAllowanceTaperRate)];
        decimal cumulative = _p.PersonalAllowance;
        foreach (TaxBand b in bands)
        {
            if (b.Width is { } w)
            {
                cumulative += w;
                points.Add(cumulative);
            }
        }

        decimal next = decimal.MaxValue;
        foreach (decimal p in points)
        {
            if (p > grossIncome && p < next)
            {
                next = p;
            }
        }

        return next;
    }

    private static decimal RateForTaxable(IReadOnlyList<TaxBand> bands, decimal taxable)
    {
        decimal cumulative = 0m;
        foreach (TaxBand b in bands)
        {
            if (b.Width is null || taxable < cumulative + b.Width.Value)
            {
                return b.Rate;
            }

            cumulative += b.Width.Value;
        }

        return bands[^1].Rate;
    }

    private decimal PersonalSavingsAllowanceFor(decimal totalTaxableIncome, IReadOnlyList<TaxBand> bands)
    {
        decimal basicLimit = bands[0].Width!.Value;
        decimal higherLimit = basicLimit + bands[1].Width!.Value;
        if (totalTaxableIncome > higherLimit)
        {
            return 0m;
        }

        return totalTaxableIncome > basicLimit ? _p.PersonalSavingsAllowanceHigher : _p.PersonalSavingsAllowanceBasic;
    }

    private static IReadOnlyList<TaxBand> Extend(IReadOnlyList<TaxBand> bands, decimal extension, TaxRegime regime)
    {
        if (extension <= 0m)
        {
            return bands;
        }

        // Relief at source extends the basic-rate limit and the higher-rate limit (i.e. the band in which 40%/42% starts and ends).
        // For Scotland the extension applies to the intermediate-rate limit (the last band below the higher rate) in the same way.
        List<TaxBand> result = [.. bands];
        int basicIndex = regime == TaxRegime.Scotland ? 2 : 0;
        result[basicIndex] = result[basicIndex] with { Width = result[basicIndex].Width!.Value + extension };
        return result;
    }

    private static void Place(List<TaxLine> lines, string category, IReadOnlyList<TaxBand> bands, decimal amount, ref decimal bandPosition, ref decimal marginal)
    {
        if (amount <= 0m)
        {
            return;
        }

        decimal cumulative = 0m;
        decimal remaining = amount;
        foreach (TaxBand band in bands)
        {
            decimal bandTop = band.Width is { } w ? cumulative + w : decimal.MaxValue;
            if (bandPosition < bandTop)
            {
                decimal room = bandTop == decimal.MaxValue ? remaining : bandTop - bandPosition;
                decimal slice = Math.Min(remaining, room);
                if (slice > 0m)
                {
                    lines.Add(new TaxLine(category, band.Name, slice, band.Rate, slice * band.Rate));
                    bandPosition += slice;
                    remaining -= slice;
                    marginal = band.Rate;
                }
            }

            if (remaining <= 0m)
            {
                break;
            }

            cumulative = bandTop;
        }
    }

    private static void Validate(TaxableIncome income)
    {
        foreach ((string name, decimal value) in new[]
        {
            (nameof(income.EarnedIncome), income.EarnedIncome), (nameof(income.PensionIncome), income.PensionIncome),
            (nameof(income.OtherNonSavingsIncome), income.OtherNonSavingsIncome), (nameof(income.SavingsInterest), income.SavingsInterest),
            (nameof(income.Dividends), income.Dividends), (nameof(income.GrossPensionContributions), income.GrossPensionContributions),
            (nameof(income.ReliefAtSourceContributions), income.ReliefAtSourceContributions),
        })
        {
            if (value < 0m)
            {
                throw new ArgumentOutOfRangeException(name, value, "Income components cannot be negative.");
            }
        }

        if (income.ReliefAtSourceContributions > income.GrossPensionContributions)
        {
            throw new ArgumentException("Relief-at-source contributions cannot exceed total gross pension contributions.", nameof(income));
        }
    }
}
