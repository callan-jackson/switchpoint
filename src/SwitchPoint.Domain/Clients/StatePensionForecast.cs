using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Clients;

/// <summary>
/// The client's State Pension position: either a forecast weekly amount (from the gov.uk forecast)
/// or a count of qualifying years from which entitlement is derived.
/// </summary>
public sealed record StatePensionForecast
{
    public StatePensionForecast(decimal? forecastWeeklyAmount, int? qualifyingYears)
    {
        if (forecastWeeklyAmount is { } amount)
        {
            Guard.NonNegative(amount, nameof(forecastWeeklyAmount));
        }

        if (qualifyingYears is { } years)
        {
            Guard.InRange(years, 0, 60, nameof(qualifyingYears));
        }

        ForecastWeeklyAmount = forecastWeeklyAmount;
        QualifyingYears = qualifyingYears;
    }

    public decimal? ForecastWeeklyAmount { get; }
    public int? QualifyingYears { get; }

    public static StatePensionForecast Unknown { get; } = new(null, null);
}
