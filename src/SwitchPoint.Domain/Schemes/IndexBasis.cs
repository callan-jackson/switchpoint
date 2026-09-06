namespace SwitchPoint.Domain.Schemes;

/// <summary>Index a DB benefit is linked to in deferment or in payment.</summary>
public enum IndexBasis
{
    None = 0,
    Fixed = 1,
    Cpi = 2,
    Rpi = 3,
    /// <summary>Limited price indexation: the lesser of CPI and a cap (floor optional).</summary>
    LpiCpi = 4,
    /// <summary>Limited price indexation: the lesser of RPI and a cap (floor optional).</summary>
    LpiRpi = 5,
    /// <summary>Section 148 orders / average earnings (GMP revaluation for some leavers).</summary>
    Section148 = 6,
}

public enum SchemeFundingStatus
{
    FullyFunded = 0,
    Deficit = 1,
    PensionProtectionFund = 2,
}
