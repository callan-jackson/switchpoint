namespace SwitchPoint.Domain.Schemes;

/// <summary>The kind of existing arrangement a client holds.</summary>
public enum SchemeType
{
    PersonalPension = 0,
    StakeholderPension = 1,
    Sipp = 2,
    OccupationalMoneyPurchase = 3,
    DefinedBenefit = 4,
    Section32 = 5,
    RetirementAnnuityContract = 6,
    Isa = 7,
    GeneralInvestmentAccount = 8,
    OnshoreBond = 9,
    OffshoreBond = 10,
    DrawdownPlan = 11,
}

public static class SchemeTypeExtensions
{
    /// <summary>True for arrangements that are registered pension schemes (tax-relieved).</summary>
    public static bool IsPension(this SchemeType type) => type is SchemeType.PersonalPension or SchemeType.StakeholderPension
        or SchemeType.Sipp or SchemeType.OccupationalMoneyPurchase or SchemeType.DefinedBenefit or SchemeType.Section32
        or SchemeType.RetirementAnnuityContract or SchemeType.DrawdownPlan;

    /// <summary>True for arrangements with safeguarded benefits that require an APTA/TVC before transfer (COBS 19.1).</summary>
    public static bool HasSafeguardedBenefits(this SchemeType type) => type is SchemeType.DefinedBenefit or SchemeType.Section32;
}
