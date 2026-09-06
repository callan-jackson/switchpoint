using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Schemes;

/// <summary>A deferred defined benefit (final salary / CARE) scheme with safeguarded benefits.</summary>
public sealed class DefinedBenefitScheme : Scheme
{
    private readonly List<DbTranche> _tranches = [];

    /// <summary>For EF Core materialisation only.</summary>
    private DefinedBenefitScheme()
    {
    }

    public DefinedBenefitScheme(
        Guid id,
        Guid firmId,
        Guid clientId,
        string schemeName,
        DateOnly dateOfLeaving,
        int normalRetirementAge,
        decimal cashEquivalentTransferValue,
        DateOnly cetvDate,
        DateOnly cetvGuaranteeExpiry,
        DateTime createdAtUtc)
        : base(id, firmId, clientId, SchemeType.DefinedBenefit, schemeName, cashEquivalentTransferValue, cashEquivalentTransferValue, cetvDate, createdAtUtc)
    {
        DateOfLeaving = dateOfLeaving;
        NormalRetirementAge = Guard.InRange(normalRetirementAge, 50, 75);
        Guard.Against(cetvGuaranteeExpiry < cetvDate, "The CETV guarantee cannot expire before the CETV date.");
        CetvGuaranteeExpiry = cetvGuaranteeExpiry;
    }

    public DateOnly DateOfLeaving { get; private set; }
    public int NormalRetirementAge { get; private set; }

    /// <summary>Alias for <see cref="Scheme.TransferValue"/> in DB terminology.</summary>
    public decimal CashEquivalentTransferValue => TransferValue;

    public DateOnly CetvGuaranteeExpiry { get; private set; }
    public IReadOnlyList<DbTranche> Tranches => _tranches;

    /// <summary>Fraction of the member's pension payable to a surviving spouse (e.g. 0.5m).</summary>
    public decimal SpousePensionFraction { get; private set; } = 0.5m;

    public int GuaranteePeriodYears { get; private set; } = 5;

    /// <summary>£ of lump sum per £1 of annual pension given up.</summary>
    public decimal PclsCommutationFactor { get; private set; } = 20m;

    /// <summary>Maximum tax-free cash as a fraction of the value of benefits (HMRC standard 25%).</summary>
    public decimal MaxPclsFraction { get; private set; } = 0.25m;

    /// <summary>Reduction applied per year of early retirement before NRA (e.g. 0.04m = 4% a year).</summary>
    public decimal EarlyRetirementReductionPerYear { get; private set; } = 0.04m;

    public int? EarliestUnreducedAge { get; private set; }
    public decimal BridgingPensionAnnual { get; private set; }
    public SchemeFundingStatus FundingStatus { get; private set; } = SchemeFundingStatus.FullyFunded;

    public decimal TotalAccruedPension => _tranches.Sum(t => t.AccruedAnnualPension);

    public void ReplaceTranches(IEnumerable<DbTranche> tranches, DateTime nowUtc)
    {
        List<DbTranche> list = [.. Guard.NotNull(tranches)];
        Guard.Against(list.Count == 0, "A defined benefit scheme needs at least one tranche.");
        _tranches.Clear();
        _tranches.AddRange(list);
        Touch(nowUtc);
    }

    public void SetBenefitTerms(
        decimal spousePensionFraction,
        int guaranteePeriodYears,
        decimal pclsCommutationFactor,
        decimal maxPclsFraction,
        decimal earlyRetirementReductionPerYear,
        int? earliestUnreducedAge,
        decimal bridgingPensionAnnual,
        SchemeFundingStatus fundingStatus,
        DateTime nowUtc)
    {
        SpousePensionFraction = Guard.Fraction(spousePensionFraction);
        GuaranteePeriodYears = Guard.InRange(guaranteePeriodYears, 0, 10);
        PclsCommutationFactor = Guard.InRange(pclsCommutationFactor, 0m, 40m);
        MaxPclsFraction = Guard.InRange(maxPclsFraction, 0m, 0.25m);
        EarlyRetirementReductionPerYear = Guard.InRange(earlyRetirementReductionPerYear, 0m, 0.1m);
        if (earliestUnreducedAge is { } age)
        {
            Guard.InRange(age, 50, NormalRetirementAge, nameof(earliestUnreducedAge));
        }

        EarliestUnreducedAge = earliestUnreducedAge;
        BridgingPensionAnnual = Guard.NonNegative(bridgingPensionAnnual);
        FundingStatus = Guard.Defined(fundingStatus);
        Touch(nowUtc);
    }

    public void UpdateCetv(decimal cetv, DateOnly cetvDate, DateOnly guaranteeExpiry, DateTime nowUtc)
    {
        Guard.Against(guaranteeExpiry < cetvDate, "The CETV guarantee cannot expire before the CETV date.");
        UpdateValuation(cetv, cetv, cetvDate, nowUtc);
        CetvGuaranteeExpiry = guaranteeExpiry;
    }

    public void UpdateSchemeTerms(DateOnly dateOfLeaving, int normalRetirementAge, DateTime nowUtc)
    {
        DateOfLeaving = dateOfLeaving;
        NormalRetirementAge = Guard.InRange(normalRetirementAge, 50, 75);
        Touch(nowUtc);
    }
}
