namespace SwitchPoint.Domain.Charges;

/// <summary>How the bands of a <see cref="TieredCharge"/> are applied to a fund value.</summary>
public enum TieredChargeMode
{
    /// <summary>Each band's rate applies only to the slice of the fund that falls inside that band (the UK platform norm).</summary>
    Marginal = 0,

    /// <summary>The rate of the band containing the total fund value applies to the whole amount.</summary>
    WholeOfFund = 1,
}
