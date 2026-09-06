namespace SwitchPoint.Domain.Assumptions;

/// <summary>Broad asset classes used by the stochastic model's capital market assumptions.</summary>
public enum AssetClass
{
    UkEquity = 0,
    GlobalEquity = 1,
    GovernmentBonds = 2,
    CorporateBonds = 3,
    Property = 4,
    Cash = 5,
    Alternatives = 6,
}

public enum ProjectionBasis
{
    Nominal = 0,
    Real = 1,
}

public enum MortalityBasis
{
    /// <summary>Gompertz–Makeham approximation calibrated to ONS National Life Tables 2020–22 with 1.25% improvements.</summary>
    OnsNationalLifeTables2020_22 = 0,
    /// <summary>CMI PMA16/PFA16 with CMI projections, supplied by a licensed table file.</summary>
    CmiPma16Pfa16 = 1,
}
