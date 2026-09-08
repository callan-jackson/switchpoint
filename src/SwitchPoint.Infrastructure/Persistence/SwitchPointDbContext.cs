using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SwitchPoint.Application.Services;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Audit;
using SwitchPoint.Domain.Charges;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Common;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;
using SwitchPoint.Domain.Tenancy;
using SwitchPoint.Infrastructure.Persistence.Json;

namespace SwitchPoint.Infrastructure.Persistence;

/// <summary>Supplies the firm the current request belongs to; null when running as the system (seeding, migrations).</summary>
public interface ITenantProvider
{
    Guid? FirmId { get; }
}

/// <summary>Identity user with the claims SwitchPoint needs.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid FirmId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Adviser;
}

/// <summary>Refuses updates and deletes of audit events: the chain is append-only (ADR-0005).</summary>
public sealed class AppendOnlyAuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Check(DbContextEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is null)
        {
            return;
        }

        bool tampered = eventData.Context.ChangeTracker.Entries<AuditEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (tampered)
        {
            throw new InvalidOperationException("Audit events are append-only and cannot be modified or deleted.");
        }
    }
}

/// <summary>EF Core model for SwitchPoint. Value objects are JSON columns; tenant-scoped entities are filtered by firm.</summary>
public sealed class SwitchPointDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ITenantProvider _tenant;

    public SwitchPointDbContext(DbContextOptions<SwitchPointDbContext> options, ITenantProvider tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Firm> Firms => Set<Firm>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Scheme> Schemes => Set<Scheme>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<ModelPortfolio> ModelPortfolios => Set<ModelPortfolio>();
    public DbSet<AssumptionSet> AssumptionSets => Set<AssumptionSet>();
    public DbSet<AnalysisBase> Analyses => Set<AnalysisBase>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>Firm id used by the query filters (null disables tenant filtering for system operations).</summary>
    public Guid? CurrentFirmId => _tenant.FirmId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        foreach (Type entityType in new[] { typeof(Firm), typeof(Client), typeof(Scheme), typeof(Provider), typeof(Product), typeof(Fund), typeof(ModelPortfolio), typeof(AssumptionSet), typeof(AnalysisBase), typeof(Report) })
        {
            builder.Entity(entityType).Property(nameof(Entity.CreatedAtUtc));
            builder.Entity(entityType).Property(nameof(Entity.UpdatedAtUtc));
        }

        builder.Entity<Firm>(e =>
        {
            e.ToTable("Firms");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.FcaFirmReferenceNumber).HasMaxLength(20).IsRequired();
        });

        builder.Entity<Client>(e =>
        {
            e.ToTable("Clients");
            e.HasKey(x => x.Id);
            e.Property(x => x.FirmId);
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Title).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.NationalInsuranceNumberMasked).HasMaxLength(20);
            e.Property(x => x.AnnualSalary).HasPrecision(18, 2);
            e.Property(x => x.Address).HasConversion(Converters.Address);
            e.Property(x => x.StatePension).HasConversion(Converters.StatePension).IsRequired();
            e.Property(x => x.ExternalReference).HasConversion(Converters.ExternalReference).IsRequired();
            e.Ignore(x => x.FullName);
            e.HasIndex(x => new { x.FirmId, x.LastName, x.FirstName });
            e.HasQueryFilter(x => _tenant.FirmId == null || x.FirmId == _tenant.FirmId);
        });

        builder.Entity<Scheme>(e =>
        {
            e.ToTable("Schemes");
            e.HasKey(x => x.Id);
            e.HasDiscriminator<string>("Kind").HasValue<Scheme>("Scheme").HasValue<DefinedBenefitScheme>("DefinedBenefit");
            e.Property(x => x.FirmId);
            e.Property(x => x.ClientId);
            e.Property(x => x.Type);
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.PolicyNumber).HasMaxLength(100);
            e.Property(x => x.CurrentValue).HasPrecision(18, 2);
            e.Property(x => x.TransferValue).HasPrecision(18, 2);
            e.Property(x => x.Charges).HasConversion(Converters.ChargeSchedule, Converters.ChargeSchedule.Comparer).IsRequired();
            e.Property(x => x.Guarantees).HasConversion(Converters.Guarantees, Converters.Guarantees.Comparer).IsRequired();
            JsonField(e, "_contributions", "Contributions", Converters.Contributions);
            JsonField(e, "_holdings", "Holdings", Converters.Holdings);
            e.Ignore(x => x.Contributions);
            e.Ignore(x => x.Holdings);
            e.Ignore(x => x.AnnualContributions);
            e.HasIndex(x => new { x.FirmId, x.ClientId });
            e.HasQueryFilter(x => _tenant.FirmId == null || x.FirmId == _tenant.FirmId);
        });

        builder.Entity<DefinedBenefitScheme>(e =>
        {
            e.Property(x => x.SpousePensionFraction).HasPrecision(9, 6);
            e.Property(x => x.MaxPclsFraction).HasPrecision(9, 6);
            e.Property(x => x.EarlyRetirementReductionPerYear).HasPrecision(9, 6);
            e.Property(x => x.PclsCommutationFactor).HasPrecision(9, 4);
            e.Property(x => x.BridgingPensionAnnual).HasPrecision(18, 2);
            JsonField(e, "_tranches", "Tranches", Converters.Tranches);
            e.Ignore(x => x.Tranches);
            e.Ignore(x => x.CashEquivalentTransferValue);
            e.Ignore(x => x.TotalAccruedPension);
        });

        builder.Entity<Provider>(e =>
        {
            e.ToTable("Providers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.FcaFirmReferenceNumber).HasMaxLength(20);
            e.Property(x => x.Website).HasMaxLength(300);
            e.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProviderId);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.MinimumInvestment).HasPrecision(18, 2);
            e.Property(x => x.MinimumRegularContribution).HasPrecision(18, 2);
            JsonField(e, "_chargeVersions", "ChargeVersions", Converters.ChargeVersions);
            e.Ignore(x => x.ChargeVersions);
            e.Ignore(x => x.CurrentCharges);
            e.HasIndex(x => new { x.ProviderId, x.Name }).IsUnique();
        });

        builder.Entity<Fund>(e =>
        {
            e.ToTable("Funds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Isin);
            e.Property(x => x.Isin).HasMaxLength(12).IsRequired();
            e.Property(x => x.Sedol).HasMaxLength(7);
            e.Property(x => x.Name).HasMaxLength(300).IsRequired();
            e.Property(x => x.ShareClass).HasMaxLength(100);
            e.Property(x => x.ManagerName).HasMaxLength(200).IsRequired();
            e.Property(x => x.IaSector).HasMaxLength(100);
            e.Property(x => x.MorningstarCategory).HasMaxLength(100);
            e.Property(x => x.Ocf).HasPrecision(9, 6);
            e.Property(x => x.TransactionCosts).HasPrecision(9, 6);
            e.Property(x => x.Price).HasPrecision(18, 6);
            e.Property(x => x.FactsheetUrl).HasMaxLength(500);
            e.Property(x => x.SourceUrl).HasMaxLength(500);
            e.Property(x => x.AssetAllocation).HasConversion(Converters.AssetAllocation, Converters.AssetAllocation.Comparer).IsRequired();
            e.Property(x => x.Statistics).HasConversion(Converters.FundStatistics, Converters.FundStatistics.Comparer).IsRequired();
            e.HasIndex(x => x.Isin).IsUnique();
            e.HasIndex(x => x.IaSector);
        });

        builder.Entity<ModelPortfolio>(e =>
        {
            e.ToTable("ModelPortfolios");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProviderId);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.MpsFee).HasPrecision(9, 6);
            JsonField(e, "_holdings", "Holdings", Converters.ModelPortfolioHoldings);
            e.Ignore(x => x.Holdings);
            e.Ignore(x => x.BlendedOcf);
            e.Ignore(x => x.TotalInvestmentCharge);
            e.HasIndex(x => new { x.ProviderId, x.Name }).IsUnique();
        });

        builder.Entity<AssumptionSet>(e =>
        {
            e.ToTable("AssumptionSets");
            e.HasKey(x => x.Id);
            e.Property(x => x.IsFcaStandard);
            e.Property(x => x.FirmId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.TaxYear).HasMaxLength(10).IsRequired();
            foreach (string p in new[] { nameof(AssumptionSet.GrowthLower), nameof(AssumptionSet.GrowthIntermediate), nameof(AssumptionSet.GrowthHigher), nameof(AssumptionSet.Inflation), nameof(AssumptionSet.EarningsGrowth), nameof(AssumptionSet.RpiInflation), nameof(AssumptionSet.ChargeInflation), nameof(AssumptionSet.PreRetirementProductCharge), nameof(AssumptionSet.AnnuityExpenseLoading), nameof(AssumptionSet.StatePensionIncrease) })
            {
                e.Property(p).HasPrecision(9, 6);
            }

            e.Property(x => x.MarketInputs).HasConversion(Converters.MarketInputs, Converters.MarketInputs.Comparer).IsRequired();
            e.Property(x => x.CapitalMarketAssumptions).HasConversion(Converters.CapitalMarketAssumptions, Converters.CapitalMarketAssumptions.Comparer);
            e.HasIndex(x => new { x.FirmId, x.Name });
            e.HasQueryFilter(x => _tenant.FirmId == null || x.FirmId == null || x.FirmId == _tenant.FirmId);
        });

        builder.Entity<AnalysisBase>(e =>
        {
            e.ToTable("Analyses");
            e.HasKey(x => x.Id);
            e.HasDiscriminator<string>("Kind").HasValue<PensionSwitchAnalysis>("PensionSwitch").HasValue<DbTransferAnalysis>("DbTransfer").HasValue<CashflowPlan>("Cashflow");
            e.Property(x => x.FirmId);
            e.Property(x => x.ClientId);
            e.Property(x => x.CreatedBy);
            e.Property(x => x.ResultHash).HasMaxLength(64);
            e.Property(x => x.EngineVersion).HasMaxLength(20);
            e.Ignore(x => x.IsLocked);
            e.HasIndex(x => new { x.FirmId, x.ClientId });
            e.HasIndex(x => new { x.FirmId, x.UpdatedAtUtc });
            e.HasQueryFilter(x => _tenant.FirmId == null || x.FirmId == _tenant.FirmId);
        });

        builder.Entity<PensionSwitchAnalysis>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.ProposedAdviserCharges).HasConversion(Converters.AdviserCharge, Converters.AdviserCharge.Comparer);
            JsonField(e, "_cedingSchemeIds", "CedingSchemeIds", Converters.GuidList);
            JsonField(e, "_proposedHoldings", "ProposedHoldings", Converters.Holdings);
            e.Ignore(x => x.CedingSchemeIds);
            e.Ignore(x => x.ProposedHoldings);
            e.Ignore(x => x.IsReadyToCalculate);
        });

        builder.Entity<DbTransferAnalysis>(e =>
        {
            e.Property(x => x.DbSchemeId);
            e.Property(x => x.ProposedAdviserCharges).HasConversion(Converters.AdviserCharge, Converters.AdviserCharge.Comparer).HasColumnName("DbProposedAdviserCharges");
            e.Property(x => x.AptaGrowthRate).HasPrecision(9, 6);
            e.Property(x => x.ContingentChargingCarveOut).HasMaxLength(500);
            JsonField(e, "_proposedHoldings", "DbProposedHoldings", Converters.Holdings);
            e.Ignore(x => x.ProposedHoldings);
            e.Ignore(x => x.IsReadyToCalculate);
        });

        builder.Entity<CashflowPlan>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).HasColumnName("PlanTitle");
            e.Property(x => x.Strategy).HasConversion(Converters.PlanStrategy, Converters.PlanStrategy.Comparer);
            e.Property(x => x.StochasticSeed).HasConversion<long?>(v => v.HasValue ? unchecked((long)v.Value) : null, v => v.HasValue ? unchecked((ulong)v.Value) : null);
            JsonField(e, "_incomes", "Incomes", Converters.PlanIncomes);
            JsonField(e, "_expenses", "Expenses", Converters.PlanExpenses);
            JsonField(e, "_assets", "Assets", Converters.PlanAssets);
            JsonField(e, "_events", "Events", Converters.PlanEvents);
            e.Ignore(x => x.Incomes);
            e.Ignore(x => x.Expenses);
            e.Ignore(x => x.Assets);
            e.Ignore(x => x.Events);
        });

        builder.Entity<Report>(e =>
        {
            e.ToTable("Reports");
            e.HasKey(x => x.Id);
            foreach (string p in new[] { nameof(Report.FirmId), nameof(Report.ClientId), nameof(Report.AnalysisId), nameof(Report.AnalysisVersion), nameof(Report.AnalysisResultHash), nameof(Report.Kind), nameof(Report.Format), nameof(Report.TemplateVersion), nameof(Report.GeneratedBy), nameof(Report.Sha256), nameof(Report.StoragePath), nameof(Report.SizeBytes), nameof(Report.GeneratedAtUtc) })
            {
                e.Property(p);
            }
            e.Property(x => x.AnalysisResultHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.TemplateVersion).HasMaxLength(20).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.FirmId, x.ClientId });
            e.HasQueryFilter(x => _tenant.FirmId == null || x.FirmId == _tenant.FirmId);
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.ToTable("AuditEvents");
            e.HasKey(x => x.Id);
            foreach (string p in new[] { nameof(AuditEvent.FirmId), nameof(AuditEvent.Sequence), nameof(AuditEvent.UserId), nameof(AuditEvent.OccurredAtUtc), nameof(AuditEvent.EntityType), nameof(AuditEvent.EntityId), nameof(AuditEvent.Action), nameof(AuditEvent.PayloadJson), nameof(AuditEvent.PreviousHash), nameof(AuditEvent.Hash) })
            {
                e.Property(p);
            }
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.PreviousHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.Hash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.FirmId, x.Sequence }).IsUnique();
            e.HasIndex(x => new { x.FirmId, x.EntityId });
            e.HasQueryFilter(x => _tenant.FirmId == null || x.FirmId == _tenant.FirmId);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.HasIndex(x => x.FirmId);
        });
    }

    /// <summary>Maps a private collection field as a JSON column (DTO-mapped converter).</summary>
    private static void JsonField<TEntity, TValue, TDto>(EntityTypeBuilder<TEntity> e, string fieldName, string columnName, MappedJsonValueConverter<TValue, TDto> converter)
        where TEntity : class
        where TValue : class
    {
        e.Property<TValue>(fieldName)
            .HasColumnName(columnName)
            .HasConversion(converter, converter.Comparer)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();
    }

    /// <summary>Maps a private collection field as a JSON column (direct JSON converter).</summary>
    private static void JsonField<TEntity, TValue>(EntityTypeBuilder<TEntity> e, string fieldName, string columnName, JsonValueConverter<TValue> converter)
        where TEntity : class
        where TValue : class
    {
        e.Property<TValue>(fieldName)
            .HasColumnName(columnName)
            .HasConversion(converter, converter.Comparer)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();
    }
}

/// <summary>System tenant (no filtering) for seeding, migrations and design-time tooling.</summary>
public sealed class SystemTenantProvider : ITenantProvider
{
    public Guid? FirmId => null;
}
