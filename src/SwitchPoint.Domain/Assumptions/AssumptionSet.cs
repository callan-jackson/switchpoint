using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Assumptions;

/// <summary>Market inputs that must be refreshed from data feeds (FTSE Actuaries indices etc.) and carry an as-at date.</summary>
public sealed record MarketInputs
{
    public MarketInputs(decimal giltYieldUpTo5, decimal giltYield5To10, decimal giltYield10To15, decimal giltYieldOver15, decimal tvcAnnuityRateRpiLinked, decimal tvcAnnuityRateLevel, decimal cobs13Y, DateOnly asAt)
    {
        GiltYieldUpTo5 = Guard.InRange(giltYieldUpTo5, -0.05m, 0.2m);
        GiltYield5To10 = Guard.InRange(giltYield5To10, -0.05m, 0.2m);
        GiltYield10To15 = Guard.InRange(giltYield10To15, -0.05m, 0.2m);
        GiltYieldOver15 = Guard.InRange(giltYieldOver15, -0.05m, 0.2m);
        TvcAnnuityRateRpiLinked = Guard.InRange(tvcAnnuityRateRpiLinked, -0.05m, 0.2m);
        TvcAnnuityRateLevel = Guard.InRange(tvcAnnuityRateLevel, -0.05m, 0.2m);
        Cobs13Y = Guard.InRange(cobs13Y, -0.05m, 0.2m);
        AsAt = asAt;
    }

    public decimal GiltYieldUpTo5 { get; }
    public decimal GiltYield5To10 { get; }
    public decimal GiltYield10To15 { get; }
    public decimal GiltYieldOver15 { get; }

    /// <summary>3-month average of the COBS 13 Annex 2 3.1R(6) intermediate RPI-linked annuity rate (COBS 19 Annex 4C 1R(2)(a)).</summary>
    public decimal TvcAnnuityRateRpiLinked { get; }

    /// <summary>3-month average intermediate rate for level / fixed-increase annuities (COBS 19 Annex 4C 1R(2)(b)).</summary>
    public decimal TvcAnnuityRateLevel { get; }

    /// <summary>Y = 0.5·(ILG0 + ILG5) − 0.5%, rounded to 0.2% (COBS 13 Annex 2 3.1R(6)).</summary>
    public decimal Cobs13Y { get; }

    public DateOnly AsAt { get; }

    /// <summary>CPI-linked TVC annuity rate = RPI-linked rate + 1.0% (COBS 19 Annex 4C 1R(2)(c)).</summary>
    public decimal TvcAnnuityRateCpiLinked => TvcAnnuityRateRpiLinked + 0.01m;

    /// <summary>Gilt yield for the term band ≤5, 5–10, 10–15, &gt;15 years (COBS 19 Annex 4C 2R).</summary>
    public decimal GiltYieldForTerm(decimal years)
    {
        Guard.NonNegative(years);
        return years <= 5m ? GiltYieldUpTo5 : years <= 10m ? GiltYield5To10 : years <= 15m ? GiltYield10To15 : GiltYieldOver15;
    }
}

/// <summary>A named, versioned set of projection assumptions. FCA-standard sets are read-only.</summary>
public sealed class AssumptionSet : Entity
{
    /// <summary>For EF Core materialisation only.</summary>
    private AssumptionSet()
    {
        Name = null!;
        TaxYear = null!;
        MarketInputs = null!;
    }

    public AssumptionSet(
        Guid id,
        Guid? firmId,
        string name,
        bool isFcaStandard,
        DateTime createdAtUtc,
        decimal growthLower,
        decimal growthIntermediate,
        decimal growthHigher,
        decimal inflation,
        decimal earningsGrowth,
        decimal rpiInflation,
        decimal chargeInflation,
        decimal preRetirementProductCharge,
        decimal annuityExpenseLoading,
        int spouseAgeGapYears,
        MortalityBasis mortalityBasis,
        decimal statePensionIncrease,
        string taxYear,
        ProjectionBasis projectionBasis,
        MarketInputs marketInputs,
        CapitalMarketAssumptions? capitalMarketAssumptions = null)
        : base(id, createdAtUtc)
    {
        FirmId = firmId;
        Name = Guard.NotNullOrWhiteSpace(name);
        IsFcaStandard = isFcaStandard;
        GrowthLower = Guard.InRange(growthLower, -0.1m, 0.2m);
        GrowthIntermediate = Guard.InRange(growthIntermediate, -0.1m, 0.2m);
        GrowthHigher = Guard.InRange(growthHigher, -0.1m, 0.2m);
        Guard.Against(!(growthLower <= growthIntermediate && growthIntermediate <= growthHigher), "Growth rates must be ordered lower ≤ intermediate ≤ higher.");
        Inflation = Guard.InRange(inflation, -0.05m, 0.15m);
        EarningsGrowth = Guard.InRange(earningsGrowth, -0.05m, 0.15m);
        RpiInflation = Guard.InRange(rpiInflation, -0.05m, 0.15m);
        ChargeInflation = Guard.InRange(chargeInflation, 0m, 0.15m);
        PreRetirementProductCharge = Guard.InRange(preRetirementProductCharge, 0m, 0.05m);
        AnnuityExpenseLoading = Guard.InRange(annuityExpenseLoading, 0m, 0.2m);
        SpouseAgeGapYears = Guard.InRange(spouseAgeGapYears, 0, 10);
        MortalityBasis = Guard.Defined(mortalityBasis);
        StatePensionIncrease = Guard.InRange(statePensionIncrease, 0m, 0.15m);
        TaxYear = Guard.NotNullOrWhiteSpace(taxYear);
        ProjectionBasis = Guard.Defined(projectionBasis);
        MarketInputs = Guard.NotNull(marketInputs);
        CapitalMarketAssumptions = capitalMarketAssumptions;
    }

    public Guid? FirmId { get; }
    public string Name { get; private set; }
    public bool IsFcaStandard { get; }
    public int Version { get; private set; } = 1;
    public decimal GrowthLower { get; private set; }
    public decimal GrowthIntermediate { get; private set; }
    public decimal GrowthHigher { get; private set; }
    public decimal Inflation { get; private set; }
    public decimal EarningsGrowth { get; private set; }
    public decimal RpiInflation { get; private set; }
    public decimal ChargeInflation { get; private set; }
    public decimal PreRetirementProductCharge { get; private set; }
    public decimal AnnuityExpenseLoading { get; private set; }
    public int SpouseAgeGapYears { get; private set; }
    public MortalityBasis MortalityBasis { get; private set; }
    public decimal StatePensionIncrease { get; private set; }
    public string TaxYear { get; private set; }
    public ProjectionBasis ProjectionBasis { get; private set; }
    public MarketInputs MarketInputs { get; private set; }
    public CapitalMarketAssumptions? CapitalMarketAssumptions { get; private set; }

    /// <summary>Applies edits to a firm-owned set, bumping the version. FCA-standard sets throw.</summary>
    public void Update(Action<AssumptionSetEditor> edit, DateTime nowUtc)
    {
        Guard.Against(IsFcaStandard, "FCA-standard assumption sets are read-only; copy it into a firm set to change values.");
        AssumptionSetEditor editor = new(this);
        Guard.NotNull(edit)(editor);
        editor.Apply();
        Version++;
        Touch(nowUtc);
    }

    /// <summary>Creates an editable firm copy of this set.</summary>
    public AssumptionSet CopyForFirm(Guid newId, Guid firmId, string name, DateTime nowUtc) => new(
        newId, Guard.NotEmpty(firmId), name, false, nowUtc, GrowthLower, GrowthIntermediate, GrowthHigher, Inflation, EarningsGrowth, RpiInflation,
        ChargeInflation, PreRetirementProductCharge, AnnuityExpenseLoading, SpouseAgeGapYears, MortalityBasis, StatePensionIncrease, TaxYear, ProjectionBasis, MarketInputs, CapitalMarketAssumptions);

    /// <summary>Mutable staging object used by <see cref="Update"/> so that validation runs as a unit.</summary>
    public sealed class AssumptionSetEditor
    {
        private readonly AssumptionSet _target;

        internal AssumptionSetEditor(AssumptionSet target)
        {
            _target = target;
            Name = target.Name;
            GrowthLower = target.GrowthLower;
            GrowthIntermediate = target.GrowthIntermediate;
            GrowthHigher = target.GrowthHigher;
            Inflation = target.Inflation;
            EarningsGrowth = target.EarningsGrowth;
            RpiInflation = target.RpiInflation;
            ChargeInflation = target.ChargeInflation;
            PreRetirementProductCharge = target.PreRetirementProductCharge;
            AnnuityExpenseLoading = target.AnnuityExpenseLoading;
            SpouseAgeGapYears = target.SpouseAgeGapYears;
            MortalityBasis = target.MortalityBasis;
            StatePensionIncrease = target.StatePensionIncrease;
            TaxYear = target.TaxYear;
            ProjectionBasis = target.ProjectionBasis;
            MarketInputs = target.MarketInputs;
            CapitalMarketAssumptions = target.CapitalMarketAssumptions;
        }

        public string Name { get; set; }
        public decimal GrowthLower { get; set; }
        public decimal GrowthIntermediate { get; set; }
        public decimal GrowthHigher { get; set; }
        public decimal Inflation { get; set; }
        public decimal EarningsGrowth { get; set; }
        public decimal RpiInflation { get; set; }
        public decimal ChargeInflation { get; set; }
        public decimal PreRetirementProductCharge { get; set; }
        public decimal AnnuityExpenseLoading { get; set; }
        public int SpouseAgeGapYears { get; set; }
        public MortalityBasis MortalityBasis { get; set; }
        public decimal StatePensionIncrease { get; set; }
        public string TaxYear { get; set; }
        public ProjectionBasis ProjectionBasis { get; set; }
        public MarketInputs MarketInputs { get; set; }
        public CapitalMarketAssumptions? CapitalMarketAssumptions { get; set; }

        internal void Apply()
        {
            // Re-run constructor validation on a throwaway instance before mutating the target.
            _ = new AssumptionSet(Guid.NewGuid(), _target.FirmId, Name, false, _target.CreatedAtUtc, GrowthLower, GrowthIntermediate, GrowthHigher, Inflation, EarningsGrowth, RpiInflation,
                ChargeInflation, PreRetirementProductCharge, AnnuityExpenseLoading, SpouseAgeGapYears, MortalityBasis, StatePensionIncrease, TaxYear, ProjectionBasis, MarketInputs, CapitalMarketAssumptions);
            _target.Name = Name;
            _target.GrowthLower = GrowthLower;
            _target.GrowthIntermediate = GrowthIntermediate;
            _target.GrowthHigher = GrowthHigher;
            _target.Inflation = Inflation;
            _target.EarningsGrowth = EarningsGrowth;
            _target.RpiInflation = RpiInflation;
            _target.ChargeInflation = ChargeInflation;
            _target.PreRetirementProductCharge = PreRetirementProductCharge;
            _target.AnnuityExpenseLoading = AnnuityExpenseLoading;
            _target.SpouseAgeGapYears = SpouseAgeGapYears;
            _target.MortalityBasis = MortalityBasis;
            _target.StatePensionIncrease = StatePensionIncrease;
            _target.TaxYear = TaxYear;
            _target.ProjectionBasis = ProjectionBasis;
            _target.MarketInputs = MarketInputs;
            _target.CapitalMarketAssumptions = CapitalMarketAssumptions;
        }
    }
}
