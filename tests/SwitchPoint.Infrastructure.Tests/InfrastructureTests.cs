using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Ports;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Audit;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;
using SwitchPoint.Domain.Tenancy;
using SwitchPoint.Infrastructure;
using SwitchPoint.Infrastructure.Integrations;
using SwitchPoint.Infrastructure.Persistence;
using SwitchPoint.Infrastructure.Seeding;
using Xunit;

namespace SwitchPoint.Infrastructure.Tests;

/// <summary>Builds a full Infrastructure service provider on a temporary SQLite file.</summary>
public sealed class SqliteFixture : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"switchpoint-test-{Guid.NewGuid():N}.db");

    public SqliteFixture(bool seedDemo = true, Guid? firmId = null)
    {
        FirmId = firmId ?? DataSeeder.DemoFirmId;
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.FirmId.Returns(_ => FirmId);
        user.UserId.Returns(Guid.NewGuid());
        user.DisplayName.Returns("Test User");
        Dictionary<string, string?> settings = new()
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:MigrateOnStartup"] = "true",
            ["ConnectionStrings:SwitchPoint"] = $"Data Source={_path}",
            ["Seed:Demo"] = seedDemo ? "true" : "false",
            ["Reports:Path"] = Path.Combine(Path.GetTempPath(), $"switchpoint-reports-{Guid.NewGuid():N}"),
            ["Integrations:Intelliflo:Mode"] = "Sandbox",
            ["Integrations:OrigoHub:Mode"] = "Sandbox",
            ["Integrations:Morningstar:Mode"] = "Sandbox",
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(config);
        services.AddSingleton(user);
        services.AddSwitchPointInfrastructure(config);
        Provider = services.BuildServiceProvider();
        Provider.InitialiseDatabaseAsync(RepoRoot(), CancellationToken.None).GetAwaiter().GetResult();
    }

    public Guid FirmId { get; set; }
    public ServiceProvider Provider { get; }

    public IServiceScope Scope() => Provider.CreateScope();

    public static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SwitchPoint.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    public void Dispose()
    {
        Provider.Dispose();
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
    }
}

public sealed class PersistenceRoundTripTests : IDisposable
{
    private readonly SqliteFixture _fx = new(seedDemo: false);
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task Client_and_schemes_round_trip_with_json_value_objects()
    {
        Guid clientId = Guid.NewGuid();
        Guid schemeId = Guid.NewGuid();
        Guid dbId = Guid.NewGuid();
        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            Client c = new(clientId, _fx.FirmId, "Sarah", "Mitchell", new DateOnly(1971, 3, 14), Sex.Female, Now, "Ms", new ExternalReference(ExternalSource.Intelliflo, "IO-1"));
            c.UpdateContactDetails("s@example.com", null, new Address("14 Beacon Road", null, "Crowborough", "East Sussex", "TN6 1AB"), Now);
            c.UpdateCircumstances(MaritalStatus.Married, EmploymentStatus.Employed, 58_000m, 67, TaxRegime.Scotland, 5, HealthStatus.Standard, false, new StatePensionForecast(241.30m, 34), Now);
            c.SetNationalInsuranceNumber("QQ123456A", Now);
            db.Clients.Add(c);

            Scheme s = new(schemeId, _fx.FirmId, clientId, SchemeType.PersonalPension, "Legacy PP", 142_000m, 138_000m, new DateOnly(2026, 8, 20), Now, null, "PP-1");
            s.SetCharges(new ChargeSchedule
            {
                PlatformCharge = TieredCharge.Marginal((250_000m, 0.0025m), (null, 0.0015m)),
                FixedCharges = [new FixedCharge(3.5m, Frequency.Monthly, Indexation.Fixed(0.05m), FixedChargeScope.Wrapper, "Policy fee")],
                FundCharge = FundChargeBasis.Explicit(0.0075m),
                ExitPenalty = ExitPenaltySchedule.Declining((5m, 0.05m), (10m, 0.02m)),
                AllocationRate = 0.97m,
                BidOfferSpread = 0.05m,
            }, Now);
            s.SetGuarantees(new Guarantees(guaranteedAnnuityRate: 0.085m, withProfits: true), Now);
            s.ReplaceContributions([new Contribution(ContributionPayer.Member, 150m, Frequency.Monthly, 0.03m, false)], Now);
            s.ReplaceHoldings([new Holding("Fund A", 0.6m, "GB00B3X7QG63"), new Holding("Fund B", 0.4m, null, null, 0.002m)], Now);
            db.Schemes.Add(s);

            DefinedBenefitScheme dbs = new(dbId, _fx.FirmId, clientId, "ABC DB", new DateOnly(2016, 3, 31), 65, 450_000m, new DateOnly(2026, 8, 1), new DateOnly(2026, 11, 1), Now);
            dbs.ReplaceTranches([new DbTranche("GMP", 1_500m, RevaluationRule.Fixed(0.0475m), EscalationRule.None, true), new DbTranche("Post-05", 6_000m, RevaluationRule.StatutoryPost2009, EscalationRule.StatutoryPost2005)], Now);
            dbs.SetBenefitTerms(0.5m, 5, 18m, 0.25m, 0.04m, 60, 0m, SchemeFundingStatus.Deficit, Now);
            db.Schemes.Add(dbs);
            await db.SaveChangesAsync();
        }

        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            Client c = await db.Clients.SingleAsync(x => x.Id == clientId);
            Assert.Equal("******56A", c.NationalInsuranceNumberMasked);
            Assert.Equal("Crowborough", c.Address!.Town);
            Assert.Equal(241.30m, c.StatePension.ForecastWeeklyAmount);
            Assert.Equal(ExternalSource.Intelliflo, c.ExternalReference.Source);
            Assert.Equal(TaxRegime.Scotland, c.TaxRegime);

            Scheme s = await db.Schemes.SingleAsync(x => x.Id == schemeId);
            Assert.Equal(0.0025m, s.Charges.PlatformCharge!.Bands[0].AnnualRate);
            Assert.Equal(0.05m, s.Charges.ExitPenalty.Bands[0].Rate);
            Assert.Equal(0.97m, s.Charges.AllocationRate);
            Assert.True(s.Guarantees.WithProfits);
            Assert.Equal(0.085m, s.Guarantees.GuaranteedAnnuityRate);
            Assert.Single(s.Contributions);
            Assert.Equal(2, s.Holdings.Count);
            Assert.Equal("GB00B3X7QG63", s.Holdings[0].Isin);

            DefinedBenefitScheme dbs = Assert.IsType<DefinedBenefitScheme>(await db.Schemes.SingleAsync(x => x.Id == dbId));
            Assert.Equal(2, dbs.Tranches.Count);
            Assert.True(dbs.Tranches[0].IsGmp);
            Assert.Equal(0.0475m, dbs.Tranches[0].Revaluation.Rate);
            Assert.Equal(IndexBasis.LpiCpi, dbs.Tranches[1].Escalation.Basis);
            Assert.Equal(60, dbs.EarliestUnreducedAge);
            Assert.Equal(SchemeFundingStatus.Deficit, dbs.FundingStatus);
        }
    }

    [Fact]
    public async Task Catalogue_entities_and_assumption_sets_round_trip()
    {
        Guid productId = Guid.NewGuid();
        Guid fundId = Guid.NewGuid();
        Guid setId = Guid.NewGuid();
        Guid mpsId = Guid.NewGuid();
        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            Provider provider = new(Guid.NewGuid(), "Test Platform", ProviderKind.Platform, Now);
            db.Providers.Add(provider);
            Product product = new(productId, provider.Id, "Test SIPP", WrapperTypes.Sipp | WrapperTypes.Isa, Now, 1_000m, 50m, true);
            product.AddChargeVersion(new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.003m) }, new DateOnly(2025, 1, 1), "https://a", new DateOnly(2025, 1, 1), DataQuality.Verified, Now);
            product.AddChargeVersion(new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.0025m) }, new DateOnly(2026, 4, 6), "https://b", new DateOnly(2026, 4, 6), DataQuality.Verified, Now);
            db.Products.Add(product);
            Fund fund = new(fundId, "US0378331005", "Test Fund", "Test Co", FundType.Oeic, 0.0022m, new AssetAllocation(0.6m, 0.4m, 0m, 0m, 0m), Now, srri: 4);
            fund.UpdateStatistics(new FundStatistics(0.08m, 0.06m, 0.07m, 0.09m, 0.5m, -0.12m, 0.02m, 4, "Gold"), 250.12m, new DateOnly(2026, 9, 5), Now);
            db.Funds.Add(fund);
            ModelPortfolio mps = new(mpsId, provider.Id, "Balanced", 5, 0.0015m, Now);
            mps.ReplaceHoldings([new ModelPortfolioHolding(fundId, "US0378331005", "Test Fund", 1m, 0.0022m)], Now);
            db.ModelPortfolios.Add(mps);
            AssumptionSet set = new(setId, null, "FCA test", true, Now, 0.02m, 0.05m, 0.08m, 0.02m, 0.035m, 0.03m, 0.02m, 0.004m, 0.04m, 3, MortalityBasis.OnsNationalLifeTables2020_22, 0.035m, "2026/27", ProjectionBasis.Real,
                new MarketInputs(0.042m, 0.044m, 0.046m, 0.047m, 0.008m, 0.04m, 0.01m, new DateOnly(2026, 8, 15)),
                new CapitalMarketAssumptions([new AssetClassAssumption(AssetClass.GlobalEquity, 0.065m, 0.15m), new AssetClassAssumption(AssetClass.Cash, 0.03m, 0.01m)], new decimal[,] { { 1m, 0.1m }, { 0.1m, 1m } }, new DateOnly(2026, 1, 1), "test"));
            db.AssumptionSets.Add(set);
            await db.SaveChangesAsync();
        }

        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            Product p = await db.Products.SingleAsync(x => x.Id == productId);
            Assert.Equal(2, p.ChargeVersions.Count);
            Assert.Equal(new DateOnly(2026, 4, 5), p.ChargeVersions[0].EffectiveTo);
            Assert.Equal(0.0025m, p.CurrentCharges!.Charges.PlatformCharge!.Bands[0].AnnualRate);
            Fund f = await db.Funds.SingleAsync(x => x.Id == fundId);
            Assert.Equal("Gold", f.Statistics.MedalistRating);
            Assert.Equal(0.6m, f.AssetAllocation.Equity);
            ModelPortfolio m = await db.ModelPortfolios.SingleAsync(x => x.Id == mpsId);
            Assert.Equal(0.0037m, m.TotalInvestmentCharge);
            AssumptionSet s = await db.AssumptionSets.SingleAsync(x => x.Id == setId);
            Assert.Equal(0.044m, s.MarketInputs.GiltYield5To10);
            Assert.Equal(0.1m, s.CapitalMarketAssumptions!.Correlations[0, 1]);
        }
    }

    [Fact]
    public async Task Analyses_round_trip_with_results_and_plan_assets()
    {
        Guid clientId = Guid.NewGuid();
        Guid switchId = Guid.NewGuid();
        Guid planId = Guid.NewGuid();
        Guid setId = Guid.NewGuid();
        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            PensionSwitchAnalysis a = new(switchId, _fx.FirmId, clientId, setId, Guid.NewGuid(), "Switch", 67, Now);
            a.SetCedingSchemes([Guid.NewGuid(), Guid.NewGuid()], Now);
            a.SetProposal(Guid.NewGuid(), 1, [new Holding("F", 1m, null, null, 0.002m)], null, new AdviserCharge(0.02m, 0m, 0.0075m, 0m), Now);
            a.RecordResult("{\"criticalYield\":0.031}", new string('a', 64), "1.0.0", Now);
            db.Analyses.Add(a);
            CashflowPlan p = new(planId, _fx.FirmId, clientId, setId, Guid.NewGuid(), "Plan", Now);
            p.ReplaceAssets([new PlanAsset("SIPP", PlanAssetKind.UncrystallisedPension, 250_000m, 0.05m, new ChargeSchedule { PlatformCharge = TieredCharge.Flat(0.0025m) }, null, 0m, 6_000m, 4_000m, false)], Now);
            p.ReplaceIncomes([new PlanIncome("Salary", IncomeKind.Employment, 55_000m, 44, null, 0.035m)], Now);
            p.SetStochasticSettings(ulong.MaxValue, 500, Now);
            db.Analyses.Add(p);
            await db.SaveChangesAsync();
        }

        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            PensionSwitchAnalysis a = await db.Analyses.OfType<PensionSwitchAnalysis>().SingleAsync(x => x.Id == switchId);
            Assert.Equal(2, a.CedingSchemeIds.Count);
            Assert.Equal(0.0075m, a.ProposedAdviserCharges.OngoingRate);
            Assert.Equal(AnalysisStatus.Calculated, a.Status);
            Assert.Equal(1, a.Version);
            Assert.Equal(new string('a', 64), a.ResultHash);
            CashflowPlan p = await db.Analyses.OfType<CashflowPlan>().SingleAsync(x => x.Id == planId);
            Assert.Equal(0.0025m, p.Assets[0].Charges.PlatformCharge!.Bands[0].AnnualRate);
            Assert.Equal(55_000m, p.Incomes[0].AnnualAmount);
            Assert.Equal(ulong.MaxValue, p.StochasticSeed);
            Assert.Equal(500, p.StochasticPaths);
        }
    }

    [Fact]
    public void Every_entity_maps_its_read_only_properties()
    {
        using IServiceScope scope = _fx.Scope();
        SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
        foreach (Type t in new[] { typeof(Firm), typeof(Client), typeof(Scheme), typeof(Product), typeof(Fund), typeof(AssumptionSet), typeof(AnalysisBase), typeof(Report) })
        {
            Microsoft.EntityFrameworkCore.Metadata.IEntityType et = db.Model.FindEntityType(t)!;
            Assert.NotNull(et.FindProperty(nameof(Entity.CreatedAtUtc)));
            Assert.NotNull(et.FindProperty(nameof(Entity.UpdatedAtUtc)));
        }

        Assert.NotNull(db.Model.FindEntityType(typeof(AssumptionSet))!.FindProperty(nameof(AssumptionSet.IsFcaStandard)));
        Assert.NotNull(db.Model.FindEntityType(typeof(AnalysisBase))!.FindProperty(nameof(AnalysisBase.CreatedBy)));
        Assert.NotNull(db.Model.FindEntityType(typeof(Scheme))!.FindProperty(nameof(Scheme.Type)));
    }

    [Fact]
    public async Task Tenant_query_filter_hides_other_firms()
    {
        Guid otherFirm = Guid.NewGuid();
        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            db.Clients.Add(new Client(Guid.NewGuid(), _fx.FirmId, "Mine", "One", new DateOnly(1980, 1, 1), Sex.Male, Now));
            db.Clients.Add(new Client(Guid.NewGuid(), otherFirm, "Theirs", "Two", new DateOnly(1980, 1, 1), Sex.Male, Now));
            await db.SaveChangesAsync();
        }

        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            Assert.Equal(1, await db.Clients.CountAsync());
            Assert.Equal(2, await db.Clients.IgnoreQueryFilters().CountAsync());
            IClientRepository repo = scope.ServiceProvider.GetRequiredService<IClientRepository>();
            Assert.Null(await repo.GetAsync(otherFirm, (await db.Clients.IgnoreQueryFilters().SingleAsync(c => c.FirmId == otherFirm)).Id));
        }
    }
}

public sealed class AuditLogTests : IDisposable
{
    private readonly SqliteFixture _fx = new(seedDemo: false);

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task Append_verify_tamper_and_append_only()
    {
        using (IServiceScope scope = _fx.Scope())
        {
            IAuditLog audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            IUnitOfWork uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await audit.AppendAsync(_fx.FirmId, Guid.NewGuid(), "Client", Guid.NewGuid(), "Created", new { Name = "A" });
            await audit.AppendAsync(_fx.FirmId, Guid.NewGuid(), "Client", Guid.NewGuid(), "Updated", new { Name = "B" });
            await uow.SaveChangesAsync();
            await audit.AppendAsync(_fx.FirmId, null, "Report", null, "Generated", null);
            await uow.SaveChangesAsync();
            ChainVerification v = await audit.VerifyAsync(_fx.FirmId);
            Assert.True(v.IsValid, v.Reason ?? "valid");
            Assert.Equal(3, v.EventsChecked);
            Page<AuditEvent> page = await audit.QueryAsync(_fx.FirmId, null, "Client", 1, 10);
            Assert.Equal(2, page.Total);
            Assert.Equal(1, page.Items[0].Sequence);
        }

        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            AuditEvent e = await db.AuditEvents.IgnoreQueryFilters().OrderBy(x => x.Sequence).Skip(1).FirstAsync();
            db.Entry(e).State = EntityState.Deleted;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            string hacked = "{\"name\":\"HACKED\"}";
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE AuditEvents SET PayloadJson = {hacked} WHERE Sequence = 1");
        }

        using (IServiceScope scope = _fx.Scope())
        {
            IAuditLog audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            ChainVerification v = await audit.VerifyAsync(_fx.FirmId);
            Assert.False(v.IsValid);
            Assert.Equal(1, v.FirstBrokenIndex);
        }
    }
}

public sealed class SeedingAndIdentityTests : IDisposable
{
    private readonly SqliteFixture _fx = new(seedDemo: true);

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task Seeder_is_idempotent_and_demo_login_works()
    {
        int providers;
        int products;
        int funds;
        int clients;
        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            providers = await db.Providers.CountAsync();
            products = await db.Products.CountAsync();
            funds = await db.Funds.CountAsync();
            clients = await db.Clients.CountAsync();
            Assert.True(providers >= 6);
            Assert.True(products >= 6);
            Assert.True(funds >= 6);
            Assert.Equal(3, clients);
            Assert.True(await db.AssumptionSets.AnyAsync(a => a.IsFcaStandard));
            Guid sarah = (await db.Clients.SingleAsync(c => c.FirstName == "Sarah")).Id;
            Guid david = (await db.Clients.SingleAsync(c => c.FirstName == "David")).Id;
            Assert.Equal(2, await db.Schemes.CountAsync(x => x.ClientId == sarah));
            Assert.Equal(2, await db.Schemes.CountAsync(x => x.ClientId == david));
            Assert.Single(await db.Schemes.OfType<DefinedBenefitScheme>().ToListAsync());
        }

        await _fx.Provider.InitialiseDatabaseAsync(SqliteFixture.RepoRoot(), CancellationToken.None);

        using (IServiceScope scope = _fx.Scope())
        {
            SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
            Assert.Equal(providers, await db.Providers.CountAsync());
            Assert.Equal(products, await db.Products.CountAsync());
            Assert.Equal(funds, await db.Funds.CountAsync());
            Assert.Equal(clients, await db.Clients.CountAsync());
            IIdentityService identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            UserDto? user = await identity.AuthenticateAsync("adviser@demo.switchpoint.local", DataSeeder.DemoPassword);
            Assert.NotNull(user);
            Assert.Equal(UserRole.Adviser, user.Role);
            Assert.Equal(DataSeeder.DemoFirmId, user.FirmId);
            Assert.Equal("Demo Financial Planning Ltd", user.FirmName);
            Assert.Null(await identity.AuthenticateAsync("adviser@demo.switchpoint.local", "wrong-password"));
            Assert.NotNull(await identity.AuthenticateAsync("compliance@demo.switchpoint.local", DataSeeder.DemoPassword));
        }
    }
}

public class ReportStoreAndConnectorTests
{
    [Fact]
    public async Task Report_store_saves_loads_and_blocks_path_escape()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sp-reports-{Guid.NewGuid():N}");
        FileReportStore store = new(root);
        Guid firm = Guid.NewGuid();
        Guid report = Guid.NewGuid();
        string path = await store.SaveAsync(firm, report, "report.pdf", new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 1, 2, 3 }, (await store.LoadAsync(path)).ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(firm, report, "../evil.pdf", new byte[] { 1 }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.LoadAsync("../../etc/passwd"));
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Sandbox_connectors_return_fixtures_and_disabled_returns_nothing()
    {
        IOptions<IntegrationsOptions> options = Options.Create(new IntegrationsOptions { Intelliflo = new IntegrationOptions { Mode = IntegrationMode.Sandbox }, Xplan = new IntegrationOptions { Mode = IntegrationMode.Disabled } });
        IntellifloConnector io = new(Substitute.For<IHttpClientFactory>(), options, NullLogger<IntellifloConnector>.Instance);
        IReadOnlyList<ImportedClient> imported = await io.ImportClientsAsync(null);
        Assert.Equal(2, imported.Count);
        Assert.Equal("Intelliflo-10001", imported[0].ExternalId);
        Assert.Equal(2, imported[0].Schemes.Count);
        Assert.Single(await io.ImportClientsAsync("Intelliflo-10002"));
        Assert.NotNull(io.LastSyncUtc);
        XplanConnector xplan = new(options, NullLogger<XplanConnector>.Instance);
        Assert.Empty(await xplan.ImportClientsAsync(null));
        Assert.Equal(IntegrationMode.Disabled, xplan.Mode);
        string xml = OrigoHubConnector.BuildContractEnquiryXml("AVIVA", "PP<1>", "unipass-123");
        Assert.Contains("<PolicyNumber>PP&lt;1&gt;</PolicyNumber>", xml, StringComparison.Ordinal);
        Assert.Contains("MessageVersion>2.1<", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Morningstar_sandbox_serves_the_catalogue()
    {
        IFundCatalogue catalogue = Substitute.For<IFundCatalogue>();
        Fund fund = new(Guid.NewGuid(), "GB00B3TYHH97", "Vanguard LS60", "Vanguard", FundType.Oeic, 0.0022m, AssetAllocation.AllEquity, DateTime.UtcNow);
        catalogue.SearchAsync("Vanguard", null, null, 1, 10, Arg.Any<CancellationToken>()).Returns(new Page<Fund>([fund], 1, 10, 1));
        catalogue.GetByIsinAsync("GB00B3TYHH97", Arg.Any<CancellationToken>()).Returns(fund);
        MorningstarFundDataProvider provider = new(Substitute.For<IHttpClientFactory>(), Options.Create(new IntegrationsOptions { Morningstar = new IntegrationOptions { Mode = IntegrationMode.Sandbox } }), catalogue, NullLogger<MorningstarFundDataProvider>.Instance);
        Assert.Single(await provider.SearchAsync("Vanguard", 10));
        Assert.Equal(0.22m, (await provider.GetAsync("GB00B3TYHH97"))!.OcfPct);
        Assert.Equal(0, await provider.SyncAsync());
        Assert.Equal(IntegrationMode.Sandbox, provider.Mode);
    }

    [Fact]
    public void Data_directory_resolves_from_bin_to_repo_root()
    {
        string resolved = DependencyInjection.ResolveDataDirectory(null, AppContext.BaseDirectory);
        Assert.EndsWith("data", resolved, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(resolved, "fca-assumptions.json")));
    }
}
