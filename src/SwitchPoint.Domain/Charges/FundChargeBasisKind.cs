namespace SwitchPoint.Domain.Charges;

/// <summary>Where the fund-level ongoing charge comes from.</summary>
public enum FundChargeBasisKind
{
    /// <summary>No fund charge (e.g. a cash account).</summary>
    None = 0,

    /// <summary>A single explicit OCF supplied on the schedule.</summary>
    Explicit = 1,

    /// <summary>The weighted OCF of the holdings, supplied at calculation time.</summary>
    FromHoldings = 2,
}
