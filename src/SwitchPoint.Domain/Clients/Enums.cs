namespace SwitchPoint.Domain.Clients;

/// <summary>Biological sex as used by annuity pricing bases (PMA16/PFA16).</summary>
public enum Sex
{
    Male = 0,
    Female = 1,
}

public enum MaritalStatus
{
    Single = 0,
    Married = 1,
    CivilPartnership = 2,
    Divorced = 3,
    Widowed = 4,
    Cohabiting = 5,
}

public enum EmploymentStatus
{
    Employed = 0,
    SelfEmployed = 1,
    Retired = 2,
    NotWorking = 3,
    Director = 4,
}

/// <summary>Which income tax regime applies to the client's non-savings income.</summary>
public enum TaxRegime
{
    RestOfUk = 0,
    Scotland = 1,
}

/// <summary>Health status affecting annuity terms.</summary>
public enum HealthStatus
{
    Standard = 0,
    Enhanced = 1,
}

/// <summary>Where a client record originated.</summary>
public enum ExternalSource
{
    Manual = 0,
    Intelliflo = 1,
    Xplan = 2,
    TruePotential = 3,
    Origo = 4,
}
