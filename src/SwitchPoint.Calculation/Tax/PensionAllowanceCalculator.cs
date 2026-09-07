using SwitchPoint.Calculation.Numerics;

namespace SwitchPoint.Calculation.Tax;

/// <summary>Inputs for an annual allowance check.</summary>
/// <param name="ThresholdIncome">Net income less member contributions (plus salary-sacrificed amounts after 8 July 2015).</param>
/// <param name="AdjustedIncome">Threshold income plus all pension input (employer and member).</param>
/// <param name="TotalPensionInput">Total contributions/accrual this year.</param>
/// <param name="MoneyPurchaseInput">The money-purchase part of the input (tested against the MPAA when triggered).</param>
/// <param name="MpaaTriggered">True once the client has flexibly accessed benefits.</param>
/// <param name="UnusedAllowancePreviousYears">Unused AA from the previous three years, oldest first.</param>
public sealed record AllowanceCheckRequest(
    decimal ThresholdIncome,
    decimal AdjustedIncome,
    decimal TotalPensionInput,
    decimal MoneyPurchaseInput,
    bool MpaaTriggered,
    IReadOnlyList<decimal> UnusedAllowancePreviousYears);

public sealed record AllowanceCheckResult(
    decimal AnnualAllowance,
    bool Tapered,
    decimal CarryForwardAvailable,
    decimal CarryForwardUsed,
    decimal ExcessOverAnnualAllowance,
    decimal? ExcessOverMpaa,
    IReadOnlyList<string> Warnings);

/// <summary>Annual allowance, taper, carry-forward and MPAA tests (FA 2004 ss227–228ZA). See docs/methodology/tax.md.</summary>
public sealed class PensionAllowanceCalculator
{
    private readonly PensionAllowanceParameters _p;

    public PensionAllowanceCalculator(TaxYearParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _p = parameters.Pensions;
    }

    public decimal TaperedAnnualAllowance(decimal thresholdIncome, decimal adjustedIncome)
    {
        if (thresholdIncome <= _p.TaperThresholdIncome || adjustedIncome <= _p.TaperAdjustedIncome)
        {
            return _p.AnnualAllowance;
        }

        decimal reduction = (adjustedIncome - _p.TaperAdjustedIncome) * _p.TaperReductionRate;
        return Math.Max(_p.TaperMinimum, _p.AnnualAllowance - reduction);
    }

    public AllowanceCheckResult Check(AllowanceCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<string> warnings = [];
        decimal allowance = TaperedAnnualAllowance(request.ThresholdIncome, request.AdjustedIncome);
        bool tapered = allowance < _p.AnnualAllowance;
        if (tapered)
        {
            warnings.Add($"Annual allowance tapered to £{UkFormat.Amount(allowance)} (adjusted income £{UkFormat.Amount(request.AdjustedIncome)}).");
        }

        decimal? mpaaExcess = null;
        if (request.MpaaTriggered)
        {
            mpaaExcess = Math.Max(0m, request.MoneyPurchaseInput - _p.MoneyPurchaseAnnualAllowance);
            if (mpaaExcess > 0m)
            {
                warnings.Add($"Money purchase input £{UkFormat.Amount(request.MoneyPurchaseInput)} exceeds the MPAA of £{UkFormat.Amount(_p.MoneyPurchaseAnnualAllowance)} by £{UkFormat.Amount(mpaaExcess.Value)}; carry forward cannot be used against it.");
            }
        }

        // Carry forward: current year first, then oldest year first (PTM055100).
        decimal carryAvailable = request.UnusedAllowancePreviousYears.Take(_p.CarryForwardYears).Sum(x => Math.Max(0m, x));
        decimal testedInput = request.MpaaTriggered ? request.TotalPensionInput - request.MoneyPurchaseInput : request.TotalPensionInput;
        decimal testedAllowance = request.MpaaTriggered ? Math.Max(0m, allowance - _p.MoneyPurchaseAnnualAllowance) : allowance;
        decimal excess = Math.Max(0m, testedInput - testedAllowance);
        decimal carryUsed = Math.Min(excess, carryAvailable);
        excess -= carryUsed;
        if (excess > 0m)
        {
            warnings.Add($"Pension input exceeds the available annual allowance by £{UkFormat.Amount(excess)}; an annual allowance charge at the marginal rate applies.");
        }

        return new AllowanceCheckResult(allowance, tapered, carryAvailable, carryUsed, excess, mpaaExcess, warnings);
    }

    /// <summary>Maximum gross member contribution eligible for tax relief: the greater of £3,600 and relevant UK earnings.</summary>
    public decimal MaxRelievableContribution(decimal relevantUkEarnings) => Math.Max(_p.BasicAmountRelievable, Math.Max(0m, relevantUkEarnings));

    /// <summary>Grosses up a net relief-at-source contribution.</summary>
    public decimal GrossUpReliefAtSource(decimal netContribution) => netContribution / (1m - _p.ReliefAtSourceRate);
}
