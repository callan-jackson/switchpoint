namespace SwitchPoint.Domain.Charges;

/// <summary>How a fixed monetary charge grows over time.</summary>
public enum IndexationBasis
{
    None = 0,
    Cpi = 1,
    Fixed = 2,
}
