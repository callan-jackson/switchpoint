using SwitchPoint.Calculation.Tax;

namespace SwitchPoint.Calculation.StatePension;

/// <summary>State Pension age for a date of birth, as years and months plus the exact date it is reached.</summary>
public sealed record StatePensionAge(int Years, int Months, DateOnly ReachedOn, bool SubjectToReview)
{
    public decimal AsDecimalYears => Years + (Months / 12m);
}

/// <summary>
/// State Pension age (Pensions Acts 2007, 2011 and 2014; gov.uk timetable) and new State Pension entitlement.
/// See docs/methodology/state-pension.md.
/// </summary>
public sealed class StatePensionCalculator
{
    private static readonly DateOnly Phase66To67Start = new(1960, 4, 6);
    private static readonly DateOnly Phase66To67End = new(1961, 3, 6);   // exclusive: born on/after this reach SPA at 67
    private static readonly DateOnly Age68Start = new(1977, 4, 6);
    private static readonly DateOnly Phase67To68End = new(1978, 3, 6);   // exclusive: born on/after this reach SPA at 68

    private readonly TaxYearParameters _p;

    public StatePensionCalculator(TaxYearParameters parameters)
    {
        _p = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>State Pension age for someone born on <paramref name="dateOfBirth"/>.</summary>
    public static StatePensionAge StatePensionAgeFor(DateOnly dateOfBirth)
    {
        if (dateOfBirth < Phase66To67Start)
        {
            return Build(dateOfBirth, 66, 0, false);
        }

        if (dateOfBirth < Phase66To67End)
        {
            return Build(dateOfBirth, 66, MonthsIntoPhase(dateOfBirth, Phase66To67Start), false);
        }

        if (dateOfBirth < Age68Start)
        {
            return Build(dateOfBirth, 67, 0, false);
        }

        if (dateOfBirth < Phase67To68End)
        {
            return Build(dateOfBirth, 67, MonthsIntoPhase(dateOfBirth, Age68Start), true);
        }

        return Build(dateOfBirth, 68, 0, true);
    }

    /// <summary>
    /// Annual new State Pension: the client's forecast if supplied, else pro rata on qualifying years
    /// (35 for the full rate, nothing below 10).
    /// </summary>
    public decimal AnnualEntitlement(decimal? forecastWeeklyAmount, int? qualifyingYears)
    {
        if (forecastWeeklyAmount is { } weekly)
        {
            return weekly * 52m;
        }

        if (qualifyingYears is not { } years || years < _p.StatePensionMinimumQualifyingYears)
        {
            return 0m;
        }

        decimal fraction = Math.Min(1m, years / (decimal)_p.StatePensionQualifyingYearsForFull);
        return _p.FullNewStatePensionWeekly * 52m * fraction;
    }

    private static int MonthsIntoPhase(DateOnly dob, DateOnly phaseStart)
    {
        // Each month of birth after the phase start adds one month to SPA: 6 Apr–5 May 1960 → +1 month, 6 May–5 Jun → +2, ...
        int months = ((dob.Year - phaseStart.Year) * 12) + dob.Month - phaseStart.Month;
        if (dob.Day < phaseStart.Day)
        {
            months--;
        }

        return months + 1;
    }

    private static StatePensionAge Build(DateOnly dob, int years, int months, bool review)
    {
        DateOnly reached = AddYearsAndMonths(dob, years, months);
        return new StatePensionAge(years, months, reached, review);
    }

    /// <summary>Adds whole years and months; if the resulting day does not exist the date rolls to the first of the following month (DWP practice).</summary>
    private static DateOnly AddYearsAndMonths(DateOnly date, int years, int months)
    {
        int totalMonths = (date.Year * 12) + (date.Month - 1) + (years * 12) + months;
        int year = totalMonths / 12;
        int month = (totalMonths % 12) + 1;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        return date.Day <= daysInMonth ? new DateOnly(year, month, date.Day) : new DateOnly(year, month, daysInMonth).AddDays(1);
    }
}
