namespace SwitchPoint.Domain.Market;

/// <summary>Tax wrappers a product can hold (flags).</summary>
[Flags]
public enum WrapperTypes
{
    None = 0,
    Sipp = 1,
    PersonalPension = 2,
    Isa = 4,
    GeneralInvestmentAccount = 8,
    OnshoreBond = 16,
    OffshoreBond = 32,
    Drawdown = 64,
    JuniorIsa = 128,
}

public enum DataQuality
{
    /// <summary>Checked against the provider's published charge sheet on the recorded date.</summary>
    Verified = 0,
    /// <summary>Entered from secondary sources or older sheets; use with a warning.</summary>
    Indicative = 1,
    /// <summary>Illustrative only; never used for a client recommendation.</summary>
    Placeholder = 2,
}

public enum FundUniverse
{
    WholeOfMarket = 0,
    Restricted = 1,
}
