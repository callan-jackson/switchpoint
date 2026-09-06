using SwitchPoint.Application.Dtos;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Application.Mapping;

/// <summary>Entity ↔ DTO mapping for clients, schemes, catalogue items and assumption sets.</summary>
public static class EntityMapping
{
    // --- Clients ---

    public static Client ToNewClient(this ClientWrite w, Guid id, Guid firmId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(w);
        Client c = new(id, firmId, w.FirstName, w.LastName, w.DateOfBirth, w.Sex, nowUtc, w.Title);
        c.ApplyWrite(w, nowUtc);
        return c;
    }

    public static void ApplyWrite(this Client c, ClientWrite w, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(w);
        c.UpdatePersonalDetails(w.Title, w.FirstName, w.LastName, w.DateOfBirth, w.Sex, nowUtc);
        c.UpdateContactDetails(w.Email, w.Phone, w.Address is null ? null : new Address(w.Address.Line1, w.Address.Line2, w.Address.Town, w.Address.County, w.Address.Postcode, w.Address.Country ?? "United Kingdom"), nowUtc);
        c.UpdateCircumstances(w.MaritalStatus, w.EmploymentStatus, w.AnnualSalary, w.TargetRetirementAge, w.TaxRegime, w.RiskProfile, w.Health, w.IsSmoker,
            new StatePensionForecast(w.StatePension.ForecastWeeklyAmount, w.StatePension.QualifyingYears), nowUtc);
        if (!string.IsNullOrWhiteSpace(w.NationalInsuranceNumber))
        {
            c.SetNationalInsuranceNumber(w.NationalInsuranceNumber, nowUtc);
        }
    }

    public static ClientDetail ToDetail(this Client c, IReadOnlyList<SchemeDto> schemes, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new ClientDetail
        {
            Id = c.Id,
            Title = c.Title,
            FirstName = c.FirstName,
            LastName = c.LastName,
            FullName = c.FullName,
            DateOfBirth = c.DateOfBirth,
            Age = c.AgeOn(today),
            Sex = c.Sex,
            Email = c.Email,
            Phone = c.Phone,
            Address = c.Address is null ? null : new AddressDto(c.Address.Line1, c.Address.Line2, c.Address.Town, c.Address.County, c.Address.Postcode, c.Address.Country),
            MaritalStatus = c.MaritalStatus,
            EmploymentStatus = c.EmploymentStatus,
            AnnualSalary = c.AnnualSalary,
            TargetRetirementAge = c.TargetRetirementAge,
            TaxRegime = c.TaxRegime,
            RiskProfile = c.RiskProfile,
            Health = c.Health,
            IsSmoker = c.IsSmoker,
            StatePension = new StatePensionDto(c.StatePension.ForecastWeeklyAmount, c.StatePension.QualifyingYears),
            NationalInsuranceNumberMasked = c.NationalInsuranceNumberMasked,
            ExternalReference = new ExternalReferenceDto(c.ExternalReference.Source, c.ExternalReference.ExternalId),
            Schemes = schemes,
            CreatedAtUtc = c.CreatedAtUtc,
            UpdatedAtUtc = c.UpdatedAtUtc,
        };
    }

    public static ClientSummary ToSummary(this Client c, int schemeCount, decimal totalPensionValue, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new ClientSummary(c.Id, c.FullName, c.DateOfBirth, c.AgeOn(today), c.Email, c.RiskProfile, schemeCount, totalPensionValue, c.UpdatedAtUtc);
    }

    // --- Schemes ---

    public static Scheme ToNewScheme(this SchemeWrite w, Guid id, Guid firmId, Guid clientId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(w);
        Scheme s;
        if (w.Type == SchemeType.DefinedBenefit)
        {
            DefinedBenefitDto db = w.DefinedBenefit ?? throw new ArgumentException("A defined benefit scheme needs definedBenefit details.", nameof(w));
            s = new DefinedBenefitScheme(id, firmId, clientId, w.ProductName, db.DateOfLeaving, db.NormalRetirementAge, w.TransferValue, w.ValuationDate, db.CetvGuaranteeExpiry, nowUtc);
        }
        else
        {
            s = new Scheme(id, firmId, clientId, w.Type, w.ProductName, w.CurrentValue, w.TransferValue, w.ValuationDate, nowUtc, w.ProviderId, w.PolicyNumber);
        }

        s.ApplyWrite(w, nowUtc);
        return s;
    }

    public static void ApplyWrite(this Scheme s, SchemeWrite w, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(w);
        s.SetProduct(w.ProviderId, w.ProductName, w.PolicyNumber, w.StartDate, nowUtc);
        s.UpdateValuation(w.CurrentValue, w.TransferValue, w.ValuationDate, nowUtc);
        s.SetCharges(w.Charges.ToDomain(), nowUtc);
        s.SetGuarantees(w.Guarantees.ToDomain(), nowUtc);
        s.SetRetirementAge(w.SelectedRetirementAge, nowUtc);
        s.SetDrawdown(w.InDrawdown, nowUtc);
        s.ReplaceContributions(w.Contributions.Select(c => c.ToDomain()), nowUtc);
        s.ReplaceHoldings(w.Holdings.Select(h => h.ToDomain()), nowUtc);
        if (s is DefinedBenefitScheme db && w.DefinedBenefit is { } d)
        {
            db.UpdateSchemeTerms(d.DateOfLeaving, d.NormalRetirementAge, nowUtc);
            db.UpdateCetv(w.TransferValue, w.ValuationDate, d.CetvGuaranteeExpiry, nowUtc);
            db.ReplaceTranches(d.Tranches.Select(t => new DbTranche(t.Name, t.AccruedAnnualPension, t.Revaluation.ToRevaluation(), t.Escalation.ToEscalation(), t.IsGmp)), nowUtc);
            db.SetBenefitTerms(Pct.ToFraction(d.SpousePensionPct), d.GuaranteePeriodYears, d.PclsCommutationFactor, Pct.ToFraction(d.MaxPclsPct), Pct.ToFraction(d.EarlyRetirementReductionPct), d.EarliestUnreducedAge, d.BridgingPensionAnnual, d.FundingStatus, nowUtc);
        }
    }

    public static SchemeDto ToDto(this Scheme s, string? providerName, DateOnly today, decimal? weightedOcf)
    {
        ArgumentNullException.ThrowIfNull(s);
        return new SchemeDto
        {
            Id = s.Id,
            ClientId = s.ClientId,
            Type = s.Type,
            ProviderId = s.ProviderId,
            ProviderName = providerName,
            ProductName = s.ProductName,
            PolicyNumber = s.PolicyNumber,
            CurrentValue = s.CurrentValue,
            TransferValue = s.TransferValue,
            ValuationDate = s.ValuationDate,
            StartDate = s.StartDate,
            Charges = s.Charges.ToDto(),
            Guarantees = s.Guarantees.ToDto(),
            SelectedRetirementAge = s.SelectedRetirementAge,
            InDrawdown = s.InDrawdown,
            Contributions = [.. s.Contributions.Select(c => c.ToDto())],
            Holdings = [.. s.Holdings.Select(h => h.ToDto())],
            DefinedBenefit = s is DefinedBenefitScheme db ? db.ToDbDto() : null,
            NetTransferValue = s.NetTransferValue(today),
            WeightedOcfPct = Pct.FromFraction(weightedOcf),
            CreatedAtUtc = s.CreatedAtUtc,
            UpdatedAtUtc = s.UpdatedAtUtc,
        };
    }

    public static DefinedBenefitDto ToDbDto(this DefinedBenefitScheme db)
    {
        ArgumentNullException.ThrowIfNull(db);
        return new DefinedBenefitDto(
            db.DateOfLeaving, db.NormalRetirementAge, db.CetvGuaranteeExpiry,
            [.. db.Tranches.Select(t => new DbTrancheDto(t.Name, t.AccruedAnnualPension, t.Revaluation.ToDto(), t.Escalation.ToDto(), t.IsGmp))],
            Pct.FromFraction(db.SpousePensionFraction), db.GuaranteePeriodYears, db.PclsCommutationFactor, Pct.FromFraction(db.MaxPclsFraction),
            Pct.FromFraction(db.EarlyRetirementReductionPerYear), db.EarliestUnreducedAge, db.BridgingPensionAnnual, db.FundingStatus);
    }

    // --- Market ---

    public static ProviderDto ToDto(this Provider p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new ProviderDto(p.Id, p.Name, p.Kind, p.FcaFirmReferenceNumber, p.Website);
    }

    public static ProductSummary ToSummary(this Product p, string providerName)
    {
        ArgumentNullException.ThrowIfNull(p);
        ProductChargeVersion? current = p.CurrentCharges;
        return new ProductSummary
        {
            Id = p.Id,
            ProviderId = p.ProviderId,
            ProviderName = providerName,
            Name = p.Name,
            WrapperTypes = WrapperNames(p.WrapperTypes),
            MinimumInvestment = p.MinimumInvestment,
            AllowsFamilyLinking = p.AllowsFamilyLinking,
            FundUniverse = p.FundUniverse,
            DataQuality = current?.DataQuality ?? DataQuality.Placeholder,
            EffectiveChargePctAt100k = current is null ? null : Pct.FromFraction(current.Charges.EffectiveAnnualPercentageCharge(100_000m, 0m)),
            EffectiveChargePctAt500k = current is null ? null : Pct.FromFraction(current.Charges.EffectiveAnnualPercentageCharge(500_000m, 0m)),
            CurrentChargeVersion = current?.Version,
            AsAt = current?.AsAt,
            SourceUrl = current?.SourceUrl,
        };
    }

    public static ProductDetail ToDetail(this Product p, string providerName)
    {
        ProductSummary s = p.ToSummary(providerName);
        return new ProductDetail
        {
            Id = s.Id,
            ProviderId = s.ProviderId,
            ProviderName = s.ProviderName,
            Name = s.Name,
            WrapperTypes = s.WrapperTypes,
            MinimumInvestment = s.MinimumInvestment,
            AllowsFamilyLinking = s.AllowsFamilyLinking,
            FundUniverse = s.FundUniverse,
            DataQuality = s.DataQuality,
            EffectiveChargePctAt100k = s.EffectiveChargePctAt100k,
            EffectiveChargePctAt500k = s.EffectiveChargePctAt500k,
            CurrentChargeVersion = s.CurrentChargeVersion,
            AsAt = s.AsAt,
            SourceUrl = s.SourceUrl,
            ChargeVersions = [.. p.ChargeVersions.Select(v => new ChargeVersionDto(v.Version, v.EffectiveFrom, v.EffectiveTo, v.AsAt, v.SourceUrl, v.DataQuality, v.Charges.ToDto()))],
        };
    }

    public static IReadOnlyList<string> WrapperNames(WrapperTypes flags) =>
        [.. Enum.GetValues<WrapperTypes>().Where(w => w != Domain.Market.WrapperTypes.None && flags.HasFlag(w)).Select(w => w.ToString())];

    public static WrapperTypes ParseWrappers(IEnumerable<string> names)
    {
        WrapperTypes result = Domain.Market.WrapperTypes.None;
        foreach (string n in names)
        {
            if (Enum.TryParse(n, true, out WrapperTypes w))
            {
                result |= w;
            }
        }

        return result;
    }

    public static AssetAllocationDto ToDto(this AssetAllocation a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new AssetAllocationDto(Pct.FromFraction(a.Equity), Pct.FromFraction(a.FixedInterest), Pct.FromFraction(a.Property), Pct.FromFraction(a.Cash), Pct.FromFraction(a.Alternatives));
    }

    public static AssetAllocation ToDomain(this AssetAllocationDto d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return new AssetAllocation(Pct.ToFraction(d.EquityPct), Pct.ToFraction(d.FixedInterestPct), Pct.ToFraction(d.PropertyPct), Pct.ToFraction(d.CashPct), Pct.ToFraction(d.AlternativesPct));
    }

    public static FundDto ToDto(this Fund f)
    {
        ArgumentNullException.ThrowIfNull(f);
        FundStatistics s = f.Statistics;
        return new FundDto(f.Id, f.Isin, f.Sedol, f.Name, f.ShareClass, f.ManagerName, f.Type, f.IaSector, f.MorningstarCategory, Pct.FromFraction(f.Ocf), Pct.FromFraction(f.TransactionCosts),
            f.AssetAllocation.ToDto(), f.Srri,
            new FundStatisticsDto(Pct.FromFraction(s.Return1Y), Pct.FromFraction(s.Return3YAnnualised), Pct.FromFraction(s.Return5YAnnualised), Pct.FromFraction(s.Volatility3Y), s.Sharpe3Y, Pct.FromFraction(s.MaxDrawdown3Y), Pct.FromFraction(s.Yield), s.MorningstarRating, s.MedalistRating),
            f.Price, f.PriceDate, f.FactsheetUrl, f.SourceUrl, f.AsAt);
    }

    public static ModelPortfolioDto ToDto(this ModelPortfolio m, string providerName)
    {
        ArgumentNullException.ThrowIfNull(m);
        return new ModelPortfolioDto(m.Id, m.ProviderId, providerName, m.Name, m.RiskLevel, Pct.FromFraction(m.MpsFee), Pct.FromFraction(m.BlendedOcf), Pct.FromFraction(m.TotalInvestmentCharge),
            [.. m.Holdings.Select(h => new ModelPortfolioHoldingDto(h.FundId, h.Isin, h.Name, Pct.FromFraction(h.Weight), Pct.FromFraction(h.Ocf)))]);
    }

    // --- Assumptions ---

    public static AssumptionSetDto ToDto(this AssumptionSet a)
    {
        ArgumentNullException.ThrowIfNull(a);
        MarketInputs m = a.MarketInputs;
        return new AssumptionSetDto
        {
            Id = a.Id,
            FirmId = a.FirmId,
            Name = a.Name,
            IsFcaStandard = a.IsFcaStandard,
            Version = a.Version,
            GrowthLowerPct = Pct.FromFraction(a.GrowthLower),
            GrowthIntermediatePct = Pct.FromFraction(a.GrowthIntermediate),
            GrowthHigherPct = Pct.FromFraction(a.GrowthHigher),
            InflationPct = Pct.FromFraction(a.Inflation),
            EarningsGrowthPct = Pct.FromFraction(a.EarningsGrowth),
            RpiInflationPct = Pct.FromFraction(a.RpiInflation),
            ChargeInflationPct = Pct.FromFraction(a.ChargeInflation),
            PreRetirementProductChargePct = Pct.FromFraction(a.PreRetirementProductCharge),
            AnnuityExpenseLoadingPct = Pct.FromFraction(a.AnnuityExpenseLoading),
            SpouseAgeGapYears = a.SpouseAgeGapYears,
            MortalityBasis = a.MortalityBasis,
            StatePensionIncreasePct = Pct.FromFraction(a.StatePensionIncrease),
            TaxYear = a.TaxYear,
            ProjectionBasis = a.ProjectionBasis,
            MarketInputs = new MarketInputsDto(Pct.FromFraction(m.GiltYieldUpTo5), Pct.FromFraction(m.GiltYield5To10), Pct.FromFraction(m.GiltYield10To15), Pct.FromFraction(m.GiltYieldOver15),
                Pct.FromFraction(m.TvcAnnuityRateRpiLinked), Pct.FromFraction(m.TvcAnnuityRateLevel), Pct.FromFraction(m.Cobs13Y), m.AsAt),
        };
    }

    public static MarketInputs ToDomain(this MarketInputsDto d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return new MarketInputs(Pct.ToFraction(d.GiltYieldUpTo5Pct), Pct.ToFraction(d.GiltYield5To10Pct), Pct.ToFraction(d.GiltYield10To15Pct), Pct.ToFraction(d.GiltYieldOver15Pct),
            Pct.ToFraction(d.TvcAnnuityRateRpiLinkedPct), Pct.ToFraction(d.TvcAnnuityRateLevelPct), Pct.ToFraction(d.Cobs13YPct), d.AsAt);
    }

    public static void ApplyWrite(this AssumptionSet a, AssumptionSetWrite w, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(w);
        a.Update(e =>
        {
            e.Name = w.Name;
            e.GrowthLower = Pct.ToFraction(w.GrowthLowerPct);
            e.GrowthIntermediate = Pct.ToFraction(w.GrowthIntermediatePct);
            e.GrowthHigher = Pct.ToFraction(w.GrowthHigherPct);
            e.Inflation = Pct.ToFraction(w.InflationPct);
            e.EarningsGrowth = Pct.ToFraction(w.EarningsGrowthPct);
            e.RpiInflation = Pct.ToFraction(w.RpiInflationPct);
            e.ChargeInflation = Pct.ToFraction(w.ChargeInflationPct);
            e.PreRetirementProductCharge = Pct.ToFraction(w.PreRetirementProductChargePct);
            e.AnnuityExpenseLoading = Pct.ToFraction(w.AnnuityExpenseLoadingPct);
            e.SpouseAgeGapYears = w.SpouseAgeGapYears;
            e.MortalityBasis = w.MortalityBasis;
            e.StatePensionIncrease = Pct.ToFraction(w.StatePensionIncreasePct);
            e.TaxYear = w.TaxYear;
            e.ProjectionBasis = w.ProjectionBasis;
            e.MarketInputs = w.MarketInputs.ToDomain();
        }, nowUtc);
    }
}
