namespace SwitchPoint.Domain.Charges;

/// <summary>Which part of a product a <see cref="FixedCharge"/> attaches to.</summary>
public enum FixedChargeScope
{
    /// <summary>Applies to the wrapper regardless of phase (e.g. an annual policy fee).</summary>
    Wrapper = 0,

    /// <summary>Applies only once the plan is in drawdown.</summary>
    Drawdown = 1,

    /// <summary>Applies to SIPP administration (e.g. an annual SIPP fee).</summary>
    Sipp = 2,
}
