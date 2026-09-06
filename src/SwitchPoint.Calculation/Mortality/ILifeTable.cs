using SwitchPoint.Domain.Clients;

namespace SwitchPoint.Calculation.Mortality;

/// <summary>Survival model used by the annuity pricer. Implementations must be deterministic and pure.</summary>
public interface ILifeTable
{
    /// <summary>Name shown on reports (e.g. "Gompertz–Makeham fit to ONS NLT 2020–22, CMI-style 1.25% improvements").</summary>
    string Name { get; }

    /// <summary>Probability that a life aged <paramref name="age"/> (exact, may be fractional) in calendar year <paramref name="calendarYear"/> survives <paramref name="years"/> more years.</summary>
    decimal SurvivalProbability(Sex sex, decimal age, decimal years, int calendarYear);

    /// <summary>Complete expectation of life at <paramref name="age"/>.</summary>
    decimal LifeExpectancy(Sex sex, decimal age, int calendarYear);
}
