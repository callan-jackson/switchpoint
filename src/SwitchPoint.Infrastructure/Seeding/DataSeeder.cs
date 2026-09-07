using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Services;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;
using SwitchPoint.Domain.Tenancy;
using SwitchPoint.Infrastructure.Persistence;

namespace SwitchPoint.Infrastructure.Seeding;

/// <summary>Seed file shapes (docs/api/CONTRACT.md §Seed data files).</summary>
public sealed record ProvidersFile(IReadOnlyList<ProviderSeed> Providers);

public sealed record ProviderSeed(string Name, ProviderKind Kind, string? FcaFirmReferenceNumber, string? Website, IReadOnlyList<ProductSeed> Products);

public sealed record ProductSeed(string Name, IReadOnlyList<string> WrapperTypes, decimal MinimumInvestment, bool AllowsFamilyLinking, FundUniverse FundUniverse, DateOnly EffectiveFrom, DateOnly AsAt, string? SourceUrl, DataQuality DataQuality, ChargeScheduleDto Charges, decimal MinimumRegularContribution = 0m);

public sealed record FundsFile(IReadOnlyList<FundDto> Funds);

public sealed record ModelPortfoliosFile(IReadOnlyList<ModelPortfolioSeed> ModelPortfolios);

public sealed record ModelPortfolioSeed(string ProviderName, string Name, int RiskLevel, decimal MpsFeePct, IReadOnlyList<ModelPortfolioHoldingSeed> Holdings);

public sealed record ModelPortfolioHoldingSeed(string Isin, decimal WeightPct);

public sealed record AssumptionSetsFile(IReadOnlyList<AssumptionSetSeed> AssumptionSets);

public sealed record AssumptionSetSeed(string Name, bool IsFcaStandard, decimal GrowthLowerPct, decimal GrowthIntermediatePct, decimal GrowthHigherPct, decimal InflationPct, decimal EarningsGrowthPct, decimal RpiInflationPct, decimal ChargeInflationPct, decimal PreRetirementProductChargePct, decimal AnnuityExpenseLoadingPct, int SpouseAgeGapYears, MortalityBasis MortalityBasis, decimal StatePensionIncreasePct, string TaxYear, ProjectionBasis ProjectionBasis, MarketInputsDto MarketInputs);

public sealed record CapitalMarketAssumptionsFile(IReadOnlyList<CmaClassSeed> AssetClasses, IReadOnlyList<IReadOnlyList<decimal>> Correlations, DateOnly AsAt, string Source);

public sealed record CmaClassSeed(AssetClass AssetClass, decimal ExpectedReturnPct, decimal VolatilityPct);

/// <summary>Idempotent seeding of the market catalogue, FCA assumption sets and (optionally) a demo firm.</summary>
public sealed class DataSeeder(SwitchPointDbContext db, UserManager<ApplicationUser> users, ILogger<DataSeeder> logger)
{
    public const string DemoPassword = "Demo!Pass123";
    public static readonly Guid DemoFirmId = new("11111111-1111-1111-1111-111111111111");

    private static readonly JsonSerializerOptions FileOptions = new(JsonDefaults.Options) { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public async Task SeedAsync(string dataDirectory, bool seedDemo, CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;
        await SeedAssumptionSetsAsync(dataDirectory, now, ct);
        await SeedProvidersAndProductsAsync(dataDirectory, now, ct);
        await SeedFundsAsync(dataDirectory, now, ct);

        // Model portfolios resolve their holdings against funds and providers by querying the database, so the
        // preceding steps must be committed first; otherwise every holding is dropped as "not in catalogue".
        await db.SaveChangesAsync(ct);
        await SeedModelPortfoliosAsync(dataDirectory, now, ct);
        await db.SaveChangesAsync(ct);
        if (seedDemo)
        {
            await SeedDemoFirmAsync(now, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    private static T? ReadFile<T>(string dataDirectory, string name)
    {
        string path = Path.Combine(dataDirectory, name);
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), FileOptions);
    }

    private async Task SeedAssumptionSetsAsync(string dir, DateTime now, CancellationToken ct)
    {
        List<AssumptionSet> existing = await db.AssumptionSets.IgnoreQueryFilters().Where(a => a.FirmId == null).ToListAsync(ct);
        CapitalMarketAssumptions cma = LoadCma(dir) ?? DefaultCapitalMarketAssumptions.Illustrative;
        AssumptionSetsFile? file = ReadFile<AssumptionSetsFile>(dir, "assumption-sets.json");
        IReadOnlyList<AssumptionSetSeed> seeds = file?.AssumptionSets ?? BuiltInAssumptionSets();
        foreach (AssumptionSetSeed s in seeds)
        {
            if (existing.Any(a => a.Name == s.Name))
            {
                continue;
            }

            AssumptionSet set = new(Guid.NewGuid(), null, s.Name, s.IsFcaStandard, now,
                Pct.ToFraction(s.GrowthLowerPct), Pct.ToFraction(s.GrowthIntermediatePct), Pct.ToFraction(s.GrowthHigherPct), Pct.ToFraction(s.InflationPct), Pct.ToFraction(s.EarningsGrowthPct),
                Pct.ToFraction(s.RpiInflationPct), Pct.ToFraction(s.ChargeInflationPct), Pct.ToFraction(s.PreRetirementProductChargePct), Pct.ToFraction(s.AnnuityExpenseLoadingPct), s.SpouseAgeGapYears,
                s.MortalityBasis, Pct.ToFraction(s.StatePensionIncreasePct), s.TaxYear, s.ProjectionBasis, s.MarketInputs.ToDomain(), cma);
            await db.AssumptionSets.AddAsync(set, ct);
        }
    }

    private static CapitalMarketAssumptions? LoadCma(string dir)
    {
        CapitalMarketAssumptionsFile? f = ReadFile<CapitalMarketAssumptionsFile>(dir, "capital-market-assumptions.json");
        if (f is null)
        {
            return null;
        }

        int n = f.AssetClasses.Count;
        decimal[,] m = new decimal[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                m[i, j] = f.Correlations[i][j];
            }
        }

        return new CapitalMarketAssumptions(f.AssetClasses.Select(c => new AssetClassAssumption(c.AssetClass, Pct.ToFraction(c.ExpectedReturnPct), Pct.ToFraction(c.VolatilityPct))), m, f.AsAt, f.Source);
    }

    /// <summary>COBS 13 Annex 2 / COBS 19 Annex 4C / AS TM1 values with illustrative market inputs (see data/fca-assumptions.json).</summary>
    private static IReadOnlyList<AssumptionSetSeed> BuiltInAssumptionSets() =>
    [
        new("FCA standard 2026/27", true, 2m, 5m, 8m, 2m, 3.5m, 3m, 2m, 0.4m, 4m, 3, MortalityBasis.OnsNationalLifeTables2020_22, 3.5m, "2026/27", ProjectionBasis.Real,
            new MarketInputsDto(4.2m, 4.4m, 4.6m, 4.7m, 0.8m, 4.0m, 1.0m, new DateOnly(2026, 8, 15))),
        new("Cautious firm default", false, 1m, 4m, 7m, 2m, 3m, 3m, 2m, 0.4m, 4m, 3, MortalityBasis.OnsNationalLifeTables2020_22, 3m, "2026/27", ProjectionBasis.Real,
            new MarketInputsDto(4.2m, 4.4m, 4.6m, 4.7m, 0.8m, 4.0m, 1.0m, new DateOnly(2026, 8, 15))),
    ];

    private async Task SeedProvidersAndProductsAsync(string dir, DateTime now, CancellationToken ct)
    {
        ProvidersFile? file = ReadFile<ProvidersFile>(dir, "providers.json");
        IReadOnlyList<ProviderSeed> seeds = file?.Providers ?? BuiltInProviders();
        if (file is null)
        {
            logger.LogWarning("data/providers.json not found; seeding the built-in illustrative provider set.");
        }

        List<Provider> providers = await db.Providers.ToListAsync(ct);
        List<Product> products = await db.Products.ToListAsync(ct);
        foreach (ProviderSeed ps in seeds)
        {
            Provider? provider = providers.FirstOrDefault(p => string.Equals(p.Name, ps.Name, StringComparison.OrdinalIgnoreCase));
            if (provider is null)
            {
                provider = new Provider(Guid.NewGuid(), ps.Name, ps.Kind, now, ps.FcaFirmReferenceNumber, ps.Website);
                await db.Providers.AddAsync(provider, ct);
                providers.Add(provider);
            }

            foreach (ProductSeed prs in ps.Products)
            {
                Product? product = products.FirstOrDefault(p => p.ProviderId == provider.Id && string.Equals(p.Name, prs.Name, StringComparison.OrdinalIgnoreCase));
                WrapperTypes wrappers = EntityMapping.ParseWrappers(prs.WrapperTypes);
                if (product is null)
                {
                    product = new Product(Guid.NewGuid(), provider.Id, prs.Name, wrappers == WrapperTypes.None ? WrapperTypes.Sipp : wrappers, now, prs.MinimumInvestment, prs.MinimumRegularContribution, prs.AllowsFamilyLinking, prs.FundUniverse);
                    await db.Products.AddAsync(product, ct);
                    products.Add(product);
                }

                if (!product.ChargeVersions.Any(v => v.EffectiveFrom == prs.EffectiveFrom))
                {
                    try
                    {
                        product.AddChargeVersion(prs.Charges.ToDomain(), prs.EffectiveFrom, prs.SourceUrl, prs.AsAt, prs.DataQuality, now);
                    }
                    catch (DomainException ex)
                    {
                        logger.LogWarning(ex, "Skipping charge version for {Provider} {Product}: {Message}", ps.Name, prs.Name, ex.Message);
                    }
                }
            }
        }
    }

    private static ChargeScheduleDto Platform(decimal band1, decimal rate1, decimal band2, decimal rate2, decimal rate3, decimal fixedAnnual = 0m, decimal drawdownFee = 0m, decimal dealFund = 0m, decimal dealEtf = 0m, decimal exitFee = 0m) => new()
    {
        PlatformCharge = new TieredChargeDto(TieredChargeMode.Marginal, [new TierBandDto(band1, rate1), new TierBandDto(band2, rate2), new TierBandDto(null, rate3)]),
        FixedCharges = [.. new[]
        {
            fixedAnnual > 0m ? new FixedChargeDto(fixedAnnual, Frequency.Annually, new IndexationDto(IndexationBasis.None, 0m), FixedChargeScope.Sipp, "SIPP administration fee") : null,
            drawdownFee > 0m ? new FixedChargeDto(drawdownFee, Frequency.Annually, new IndexationDto(IndexationBasis.None, 0m), FixedChargeScope.Drawdown, "Drawdown fee") : null,
        }.Where(f => f is not null).Select(f => f!)],
        FundCharge = new FundChargeDto(FundChargeBasisKind.FromHoldings, null),
        DealingCharges = new DealingChargesDto(dealFund, dealEtf, 0, 0),
        ExitPenalty = exitFee > 0m ? new ExitPenaltyDto([new ExitPenaltyBandDto(null, 0m, exitFee)]) : new ExitPenaltyDto([]),
    };

    /// <summary>Illustrative fallback used only when data/providers.json is absent. Figures are Indicative, not verified.</summary>
    private static IReadOnlyList<ProviderSeed> BuiltInProviders()
    {
        DateOnly from = new(2026, 1, 1);
        DateOnly asAt = new(2026, 9, 1);
        string[] platformWrappers = ["Sipp", "Isa", "GeneralInvestmentAccount", "Drawdown"];
        return
        [
            new("AJ Bell Investcentre", ProviderKind.Platform, "108413", "https://www.investcentre.co.uk", [new("Investcentre SIPP", platformWrappers, 1_000m, true, FundUniverse.WholeOfMarket, from, asAt, "https://www.investcentre.co.uk", DataQuality.Indicative, Platform(250_000m, 0.20m, 1_000_000m, 0.15m, 0.10m, 0m, 0m, 0m, 3.95m))]),
            new("Aviva", ProviderKind.Platform, "119178", "https://www.aviva.co.uk", [new("Aviva Platform Pension Portfolio", platformWrappers, 500m, true, FundUniverse.WholeOfMarket, from, asAt, "https://www.aviva.co.uk", DataQuality.Indicative, Platform(50_000m, 0.40m, 250_000m, 0.35m, 0.25m))]),
            new("Transact", ProviderKind.Platform, "190856", "https://www.transact-online.co.uk", [new("Transact Personal Pension", platformWrappers, 5_000m, true, FundUniverse.WholeOfMarket, from, asAt, "https://www.transact-online.co.uk", DataQuality.Indicative, Platform(60_000m, 0.31m, 300_000m, 0.28m, 0.20m, 0m, 0m, 0m, 0m))]),
            new("abrdn", ProviderKind.Platform, "133456", "https://www.abrdn.com/adviser", [new("abrdn Wrap SIPP", platformWrappers, 0m, true, FundUniverse.WholeOfMarket, from, asAt, "https://www.abrdn.com/adviser", DataQuality.Indicative, Platform(100_000m, 0.35m, 500_000m, 0.30m, 0.20m))]),
            new("Quilter", ProviderKind.Platform, "165359", "https://platform.quilter.com", [new("Quilter Collective Retirement Account", platformWrappers, 0m, true, FundUniverse.Restricted, from, asAt, "https://platform.quilter.com", DataQuality.Indicative, Platform(25_000m, 0.35m, 250_000m, 0.25m, 0.15m))]),
            new("Fidelity Adviser Solutions", ProviderKind.Platform, "122169", "https://adviserservices.fidelity.co.uk", [new("Fidelity Pension", platformWrappers, 0m, false, FundUniverse.WholeOfMarket, from, asAt, "https://adviserservices.fidelity.co.uk", DataQuality.Indicative, Platform(250_000m, 0.25m, 1_000_000m, 0.20m, 0.15m))]),
            new("Legacy Life Assurance (illustrative)", ProviderKind.Insurer, null, null,
            [
                new("Pre-2001 Personal Pension (with-profits)", ["PersonalPension"], 0m, false, FundUniverse.Restricted, from, asAt, null, DataQuality.Placeholder, new ChargeScheduleDto
                {
                    ProductCharge = new TieredChargeDto(TieredChargeMode.WholeOfFund, [new TierBandDto(null, 1.25m)]),
                    FixedCharges = [new FixedChargeDto(3.50m, Frequency.Monthly, new IndexationDto(IndexationBasis.Fixed, 5m), FixedChargeScope.Wrapper, "Policy fee")],
                    FundCharge = new FundChargeDto(FundChargeBasisKind.Explicit, 0.75m),
                    BidOfferSpreadPct = 5m,
                    AllocationRatePct = 97m,
                    ExitPenalty = new ExitPenaltyDto([new ExitPenaltyBandDto(5m, 5m, 0m), new ExitPenaltyBandDto(10m, 2m, 0m)]),
                }),
            ]),
        ];
    }

    private async Task SeedFundsAsync(string dir, DateTime now, CancellationToken ct)
    {
        FundsFile? file = ReadFile<FundsFile>(dir, "funds.json");
        IReadOnlyList<FundDto> seeds = file?.Funds ?? BuiltInFunds();
        if (file is null)
        {
            logger.LogWarning("data/funds.json not found; seeding the built-in fund set.");
        }

        HashSet<string> existing = [.. (await db.Funds.Select(f => f.Isin).ToListAsync(ct))];
        foreach (FundDto f in seeds)
        {
            string isin = f.Isin.ToUpperInvariant();
            if (existing.Contains(isin) || !Holding.IsValidIsin(isin))
            {
                if (!Holding.IsValidIsin(isin))
                {
                    logger.LogWarning("Skipping fund {Name}: invalid ISIN {Isin}", f.Name, f.Isin);
                }

                continue;
            }

            try
            {
                Fund fund = new(Guid.NewGuid(), isin, f.Name, f.ManagerName, f.Type, Pct.ToFraction(f.OcfPct), f.AssetAllocation.ToDomain(), now, f.Sedol, f.ShareClass, f.IaSector, f.MorningstarCategory, f.Srri);
                fund.UpdateCharges(Pct.ToFraction(f.OcfPct), Pct.ToFraction(f.TransactionCostsPct), f.AsAt ?? DateOnly.FromDateTime(now), f.SourceUrl, now);
                FundStatisticsDto s = f.Statistics;
                fund.UpdateStatistics(new FundStatistics(Pct.ToFraction(s.Return1YPct), Pct.ToFraction(s.Return3YPct), Pct.ToFraction(s.Return5YPct), Pct.ToFraction(s.Volatility3YPct), s.Sharpe3Y, Pct.ToFraction(s.MaxDrawdown3YPct), Pct.ToFraction(s.YieldPct), s.MorningstarRating, s.MedalistRating), f.Price, f.PriceDate, now);
                fund.UpdateProfile(f.Name, f.ManagerName, f.ShareClass, f.Sedol, f.IaSector, f.MorningstarCategory, f.Srri, f.AssetAllocation.ToDomain(), f.FactsheetUrl, now);
                await db.Funds.AddAsync(fund, ct);
                existing.Add(isin);
            }
            catch (Exception ex) when (ex is DomainException or ArgumentException)
            {
                logger.LogWarning(ex, "Skipping fund {Name}: {Message}", f.Name, ex.Message);
            }
        }
    }

    private static FundDto F(string isin, string name, string manager, FundType type, string sector, decimal ocf, decimal eq, decimal fi, decimal prop, decimal cash, int srri) =>
        new(null, isin, null, name, "Acc", manager, type, sector, null, ocf, 0.05m, new AssetAllocationDto(eq, fi, prop, cash, 100m - eq - fi - prop - cash), srri, new FundStatisticsDto(null, null, null, null, null, null, null, null, null), null, null, null, null, new DateOnly(2026, 9, 1));

    /// <summary>Real funds with valid ISINs; OCFs are indicative when data/funds.json is absent.</summary>
    private static IReadOnlyList<FundDto> BuiltInFunds() =>
    [
        F("GB00B3TYHH97", "Vanguard LifeStrategy 60% Equity", "Vanguard", FundType.Oeic, "Mixed Investment 40-85% Shares", 0.22m, 60m, 40m, 0m, 0m, 4),
        F("GB00B4PQW151", "Vanguard LifeStrategy 80% Equity", "Vanguard", FundType.Oeic, "Mixed Investment 40-85% Shares", 0.22m, 80m, 20m, 0m, 0m, 5),
        F("GB00B3ZHN960", "Vanguard LifeStrategy 40% Equity", "Vanguard", FundType.Oeic, "Mixed Investment 20-60% Shares", 0.22m, 40m, 60m, 0m, 0m, 4),
        F("GB00B41XG308", "Vanguard LifeStrategy 100% Equity", "Vanguard", FundType.Oeic, "Global", 0.22m, 100m, 0m, 0m, 0m, 6),
        F("GB00B41YBW71", "Fundsmith Equity T Acc", "Fundsmith", FundType.Oeic, "Global", 0.94m, 98m, 0m, 0m, 2m, 5),
        F("IE00B4L5Y983", "iShares Core MSCI World UCITS ETF", "BlackRock", FundType.Etf, "Global", 0.20m, 100m, 0m, 0m, 0m, 6),
        F("IE00B3XXRP09", "Vanguard S&P 500 UCITS ETF", "Vanguard", FundType.Etf, "North America", 0.07m, 100m, 0m, 0m, 0m, 6),
    ];

    private async Task SeedModelPortfoliosAsync(string dir, DateTime now, CancellationToken ct)
    {
        ModelPortfoliosFile? file = ReadFile<ModelPortfoliosFile>(dir, "model-portfolios.json");
        if (file is null)
        {
            return;
        }

        List<Provider> providers = await db.Providers.ToListAsync(ct);
        List<ModelPortfolio> existing = await db.ModelPortfolios.ToListAsync(ct);
        List<Fund> funds = await db.Funds.ToListAsync(ct);
        foreach (ModelPortfolioSeed s in file.ModelPortfolios)
        {
            Provider? provider = providers.FirstOrDefault(p => string.Equals(p.Name, s.ProviderName, StringComparison.OrdinalIgnoreCase));
            if (provider is null)
            {
                provider = new Provider(Guid.NewGuid(), s.ProviderName, ProviderKind.DiscretionaryManager, now);
                await db.Providers.AddAsync(provider, ct);
                providers.Add(provider);
            }

            if (existing.Any(m => m.ProviderId == provider.Id && m.Name == s.Name))
            {
                continue;
            }

            List<ModelPortfolioHolding> holdings = [];
            foreach (ModelPortfolioHoldingSeed h in s.Holdings)
            {
                Fund? fund = funds.FirstOrDefault(f => string.Equals(f.Isin, h.Isin, StringComparison.OrdinalIgnoreCase));
                if (fund is null)
                {
                    logger.LogWarning("MPS {Name}: fund {Isin} not in catalogue; skipping holding", s.Name, h.Isin);
                    continue;
                }

                holdings.Add(new ModelPortfolioHolding(fund.Id, fund.Isin, fund.Name, Pct.ToFraction(h.WeightPct), fund.Ocf));
            }

            decimal total = holdings.Sum(h => h.Weight);
            if (holdings.Count == 0 || Math.Abs(total - 1m) > 0.0001m)
            {
                // Re-normalise when holdings were dropped so the portfolio stays valid.
                holdings = [.. holdings.Select(h => h with { Weight = h.Weight / total })];
                if (holdings.Count == 0)
                {
                    continue;
                }
            }

            ModelPortfolio mp = new(Guid.NewGuid(), provider.Id, s.Name, s.RiskLevel, Pct.ToFraction(s.MpsFeePct), now);
            mp.ReplaceHoldings(holdings, now);
            await db.ModelPortfolios.AddAsync(mp, ct);
            existing.Add(mp);
        }
    }

    private async Task SeedDemoFirmAsync(DateTime now, CancellationToken ct)
    {
        Firm? firm = await db.Firms.FirstOrDefaultAsync(f => f.Id == DemoFirmId, ct);
        if (firm is null)
        {
            firm = new Firm(DemoFirmId, "Demo Financial Planning Ltd", "000000", now);
            await db.Firms.AddAsync(firm, ct);
            await db.SaveChangesAsync(ct);
        }

        foreach ((string email, string name, UserRole role) in new[]
        {
            ("adviser@demo.switchpoint.local", "Alex Adviser", UserRole.Adviser),
            ("paraplanner@demo.switchpoint.local", "Priya Paraplanner", UserRole.Paraplanner),
            ("compliance@demo.switchpoint.local", "Chris Compliance", UserRole.Compliance),
        })
        {
            if (await users.FindByEmailAsync(email) is null)
            {
                ApplicationUser user = new() { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, FirmId = DemoFirmId, DisplayName = name, Role = role };
                IdentityResult result = await users.CreateAsync(user, DemoPassword);
                if (!result.Succeeded)
                {
                    logger.LogWarning("Could not create demo user {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        if (await db.Clients.IgnoreQueryFilters().AnyAsync(c => c.FirmId == DemoFirmId, ct))
        {
            return;
        }

        Provider? aviva = await db.Providers.FirstOrDefaultAsync(p => p.Name.StartsWith("Aviva"), ct);
        Provider? ajBell = await db.Providers.FirstOrDefaultAsync(p => p.Name.StartsWith("AJ Bell"), ct);

        // Client 1: Sarah Mitchell, 55, two DC plans (one legacy with a GAR) — the pension switching demo.
        Client sarah = new(Guid.NewGuid(), DemoFirmId, "Sarah", "Mitchell", new DateOnly(1971, 3, 14), Sex.Female, now, "Ms");
        sarah.UpdateContactDetails("sarah.mitchell@example.com", "07700 900123", new Address("14 Beacon Road", null, "Crowborough", "East Sussex", "TN6 1AB"), now);
        sarah.UpdateCircumstances(MaritalStatus.Married, EmploymentStatus.Employed, 58_000m, 67, TaxRegime.RestOfUk, 5, HealthStatus.Standard, false, new StatePensionForecast(241.30m, 34), now);
        sarah.SetNationalInsuranceNumber("QQ123456A", now);
        await db.Clients.AddAsync(sarah, ct);

        Scheme legacy = new(Guid.NewGuid(), DemoFirmId, sarah.Id, SchemeType.PersonalPension, "Legacy Personal Pension (with-profits)", 142_000m, 138_000m, new DateOnly(2026, 8, 20), now, null, "PP-4471902");
        legacy.SetProduct(null, "Legacy Personal Pension (with-profits)", "PP-4471902", new DateOnly(1998, 6, 1), now);
        legacy.SetCharges(new ChargeSchedule
        {
            ProductCharge = TieredCharge.Flat(0.0125m, TieredChargeMode.WholeOfFund),
            FixedCharges = [new FixedCharge(3.50m, Frequency.Monthly, Indexation.Fixed(0.05m), FixedChargeScope.Wrapper, "Policy fee")],
            FundCharge = FundChargeBasis.Explicit(0.0075m),
            BidOfferSpread = 0.05m,
            AllocationRate = 0.97m,
        }, now);
        legacy.SetGuarantees(new Guarantees(guaranteedAnnuityRate: 0.085m, withProfits: true, marketValueReductionRate: 0.03m), now);
        legacy.ReplaceContributions([new Contribution(ContributionPayer.Member, 150m, Frequency.Monthly, 0.03m, false)], now);
        legacy.SetRetirementAge(67, now);
        await db.Schemes.AddAsync(legacy, ct);

        Scheme workplace = new(Guid.NewGuid(), DemoFirmId, sarah.Id, SchemeType.OccupationalMoneyPurchase, "Employer Group Personal Pension", 96_500m, 96_500m, new DateOnly(2026, 8, 31), now, aviva?.Id, "GPP-88213");
        workplace.SetProduct(aviva?.Id, "Employer Group Personal Pension", "GPP-88213", new DateOnly(2015, 4, 6), now);
        workplace.SetCharges(new ChargeSchedule { ProductCharge = TieredCharge.Flat(0.0045m), FundCharge = FundChargeBasis.FromHoldings }, now);
        workplace.ReplaceHoldings([new Holding("Vanguard LifeStrategy 60% Equity", 1m, "GB00B3TYHH97")], now);
        workplace.ReplaceContributions([new Contribution(ContributionPayer.Member, 290m, Frequency.Monthly, 0.03m, true), new Contribution(ContributionPayer.Employer, 435m, Frequency.Monthly, 0.03m, true)], now);
        await db.Schemes.AddAsync(workplace, ct);

        // Client 2: David Okafor, 57, deferred DB scheme — the APTA/TVC demo.
        Client david = new(Guid.NewGuid(), DemoFirmId, "David", "Okafor", new DateOnly(1969, 6, 15), Sex.Male, now, "Mr");
        david.UpdateContactDetails("david.okafor@example.com", "07700 900456", new Address("3 Mill Lane", null, "Tunbridge Wells", "Kent", "TN1 2CD"), now);
        david.UpdateCircumstances(MaritalStatus.Married, EmploymentStatus.SelfEmployed, 72_000m, 65, TaxRegime.RestOfUk, 4, HealthStatus.Standard, false, new StatePensionForecast(241.30m, 35), now);
        await db.Clients.AddAsync(david, ct);

        DefinedBenefitScheme dbScheme = new(Guid.NewGuid(), DemoFirmId, david.Id, "Wealden Engineering Pension Scheme", new DateOnly(2016, 3, 31), 65, 486_000m, new DateOnly(2026, 8, 1), new DateOnly(2026, 11, 1), now);
        dbScheme.ReplaceTranches(
        [
            new DbTranche("Pre-97 GMP", 1_850m, RevaluationRule.Fixed(0.0475m), EscalationRule.None, isGmp: true),
            new DbTranche("Pre-97 excess", 3_200m, RevaluationRule.StatutoryPre2009, EscalationRule.None),
            new DbTranche("1997–2005", 9_400m, RevaluationRule.StatutoryPre2009, EscalationRule.StatutoryPre2005),
            new DbTranche("Post-2005", 6_100m, RevaluationRule.StatutoryPost2009, EscalationRule.StatutoryPost2005),
        ], now);
        dbScheme.SetBenefitTerms(0.5m, 5, 18m, 0.25m, 0.04m, null, 0m, SchemeFundingStatus.FullyFunded, now);
        await db.Schemes.AddAsync(dbScheme, ct);

        Scheme davidSipp = new(Guid.NewGuid(), DemoFirmId, david.Id, SchemeType.Sipp, "AJ Bell Investcentre SIPP", 210_000m, 210_000m, new DateOnly(2026, 8, 31), now, ajBell?.Id, "SIPP-30991");
        davidSipp.SetProduct(ajBell?.Id, "AJ Bell Investcentre SIPP", "SIPP-30991", new DateOnly(2019, 1, 10), now);
        davidSipp.SetCharges(new ChargeSchedule { PlatformCharge = TieredCharge.Marginal((250_000m, 0.0020m), (1_000_000m, 0.0015m), (null, 0.0010m)), FundCharge = FundChargeBasis.FromHoldings, AdviserCharges = new AdviserCharge(ongoingRate: 0.0075m) }, now);
        davidSipp.ReplaceHoldings([new Holding("Vanguard LifeStrategy 60% Equity", 0.7m, "GB00B3TYHH97"), new Holding("iShares Core MSCI World UCITS ETF", 0.3m, "IE00B4L5Y983")], now);
        await db.Schemes.AddAsync(davidSipp, ct);

        // Client 3: Priya Shah, 44, accumulating — the cashflow demo.
        Client priya = new(Guid.NewGuid(), DemoFirmId, "Priya", "Shah", new DateOnly(1982, 9, 2), Sex.Female, now, "Dr");
        priya.UpdateCircumstances(MaritalStatus.Single, EmploymentStatus.Employed, 84_000m, 60, TaxRegime.RestOfUk, 6, HealthStatus.Standard, false, new StatePensionForecast(null, 22), now);
        await db.Clients.AddAsync(priya, ct);
        Scheme priyaSipp = new(Guid.NewGuid(), DemoFirmId, priya.Id, SchemeType.Sipp, "Workplace SIPP", 265_000m, 265_000m, new DateOnly(2026, 8, 31), now, ajBell?.Id, "SIPP-77120");
        priyaSipp.SetCharges(new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.0025m), FundCharge = FundChargeBasis.FromHoldings }, now);
        priyaSipp.ReplaceHoldings([new Holding("Vanguard LifeStrategy 80% Equity", 1m, "GB00B4PQW151")], now);
        priyaSipp.ReplaceContributions([new Contribution(ContributionPayer.Member, 700m, Frequency.Monthly, 0.03m, true), new Contribution(ContributionPayer.Employer, 560m, Frequency.Monthly, 0.03m, true)], now);
        await db.Schemes.AddAsync(priyaSipp, ct);
        Scheme priyaIsa = new(Guid.NewGuid(), DemoFirmId, priya.Id, SchemeType.Isa, "Stocks & Shares ISA", 61_000m, 61_000m, new DateOnly(2026, 8, 31), now);
        priyaIsa.SetCharges(new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.0025m), FundCharge = FundChargeBasis.FromHoldings }, now);
        priyaIsa.ReplaceHoldings([new Holding("Vanguard LifeStrategy 100% Equity", 1m, "GB00B41XG308")], now);
        await db.Schemes.AddAsync(priyaIsa, ct);
    }
}
