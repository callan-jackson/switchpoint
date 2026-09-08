using Azure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using SwitchPoint.Application.Ports;
using SwitchPoint.Infrastructure.Integrations;
using SwitchPoint.Infrastructure.Persistence;
using SwitchPoint.Infrastructure.Seeding;

namespace SwitchPoint.Infrastructure;

/// <summary>Database settings (section Database).</summary>
public sealed class DatabaseOptions
{
    public const string SqlServer = "SqlServer";
    public const string Sqlite = "Sqlite";

    public string Provider { get; set; } = Sqlite;
    public bool MigrateOnStartup { get; set; } = true;
}

/// <summary>Seed settings (section Seed).</summary>
public sealed class SeedOptions
{
    public bool Demo { get; set; }
    public string? DataDirectory { get; set; }
}

/// <summary>Report storage settings (section Reports).</summary>
public sealed class ReportStoreOptions
{
    public string Path { get; set; } = "./data/reports";
}

public static class DependencyInjection
{
    /// <summary>Adds Azure Key Vault as a configuration source when KeyVault:Uri is set (managed identity or developer credentials).</summary>
    public static IConfigurationBuilder AddSwitchPointKeyVault(this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        IConfigurationRoot current = builder.Build();
        string? uri = current["KeyVault:Uri"];
        if (!string.IsNullOrWhiteSpace(uri))
        {
            builder.AddAzureKeyVault(new Uri(uri), new DefaultAzureCredential());
        }

        return builder;
    }

    public static IServiceCollection AddSwitchPointInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<SeedOptions>(configuration.GetSection("Seed"));
        services.Configure<ReportStoreOptions>(configuration.GetSection("Reports"));
        services.Configure<IntegrationsOptions>(configuration.GetSection("Integrations"));

        services.AddSingleton<AppendOnlyAuditInterceptor>();
        services.AddDbContext<SwitchPointDbContext>((sp, o) =>
        {
            // Read configuration through the provider so that late sources (Key Vault, test overrides) are honoured.
            IConfiguration current = sp.GetRequiredService<IConfiguration>();
            DatabaseOptions dbOptions = current.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
            string connectionString = current.GetConnectionString("SwitchPoint") ?? "Data Source=switchpoint.db";
            o.AddInterceptors(sp.GetRequiredService<AppendOnlyAuditInterceptor>());
            if (string.Equals(dbOptions.Provider, DatabaseOptions.SqlServer, StringComparison.OrdinalIgnoreCase))
            {
                o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5).MigrationsAssembly(typeof(SwitchPointDbContext).Assembly.FullName));
            }
            else
            {
                o.UseSqlite(connectionString);
            }
        });

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength = 10;
                o.Password.RequireNonAlphanumeric = true;
                o.User.RequireUniqueEmail = true;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<SwitchPointDbContext>();

        services.AddScoped<ITenantProvider, CurrentUserTenantProvider>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IFirmRepository, FirmRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<ISchemeRepository, SchemeRepository>();
        services.AddScoped<IProviderCatalogue, ProviderCatalogue>();
        services.AddScoped<IProductCatalogue, ProductCatalogue>();
        services.AddScoped<IFundCatalogue, FundCatalogue>();
        services.AddScoped<IModelPortfolioCatalogue, ModelPortfolioCatalogue>();
        services.AddScoped<IAssumptionSetRepository, AssumptionSetRepository>();
        services.AddScoped<IAnalysisRepository, AnalysisRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IAuditLog, HashChainAuditLog>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<DataSeeder>();
        services.AddSingleton<IReportStore>(sp =>
        {
            ReportStoreOptions o = sp.GetRequiredService<IConfiguration>().GetSection("Reports").Get<ReportStoreOptions>() ?? new ReportStoreOptions();
            return new FileReportStore(o.Path);
        });

        services.AddHttpClient("Intelliflo").AddStandardResilienceHandler();
        services.AddHttpClient("Morningstar").AddStandardResilienceHandler();
        services.AddScoped<IBackOfficeConnector, IntellifloConnector>();
        services.AddScoped<IBackOfficeConnector, XplanConnector>();
        services.AddScoped<IBackOfficeConnector, TruePotentialConnector>();
        services.AddScoped<IBackOfficeConnector, OrigoHubConnector>();
        services.AddScoped<IFundDataProvider, MorningstarFundDataProvider>();

        return services;
    }

    /// <summary>Creates or migrates the database and runs the seeders. Call once at startup.</summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider provider, string contentRootPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        using IServiceScope scope = provider.CreateScope();
        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SwitchPoint.Infrastructure.Initialise");
        SwitchPointDbContext db = scope.ServiceProvider.GetRequiredService<SwitchPointDbContext>();
        DatabaseOptions dbOptions = configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
        SeedOptions seed = configuration.GetSection("Seed").Get<SeedOptions>() ?? new SeedOptions();

        if (db.Database.IsSqlServer() && db.Database.GetMigrations().Any())
        {
            logger.LogInformation("Applying SQL Server migrations");
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Ensuring database schema exists ({Provider})", db.Database.ProviderName);
            }

            await db.Database.EnsureCreatedAsync(ct);
        }

        string dataDirectory = ResolveDataDirectory(seed.DataDirectory, contentRootPath);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Seeding from {DataDirectory} (demo: {Demo})", dataDirectory, seed.Demo);
        }

        DataSeeder seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync(dataDirectory, seed.Demo, ct);
    }

    /// <summary>Finds the repository data/ folder from the content root or its ancestors (works from bin/ and from the repo root).</summary>
    public static string ResolveDataDirectory(string? configured, string contentRootPath)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        DirectoryInfo? dir = new(contentRootPath);
        for (int i = 0; i < 6 && dir is not null; i++)
        {
            string candidate = Path.Combine(dir.FullName, "data");
            if (Directory.Exists(candidate) && (File.Exists(Path.Combine(candidate, "providers.json")) || File.Exists(Path.Combine(candidate, "fca-assumptions.json"))))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return Path.Combine(contentRootPath, "data");
    }
}

/// <summary>Lets 'dotnet ef migrations add' build the SQL Server model without the API host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SwitchPointDbContext>
{
    public SwitchPointDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<SwitchPointDbContext> builder = new();
        builder.UseSqlServer("Server=localhost;Database=switchpoint;Trusted_Connection=True;TrustServerCertificate=True", sql => sql.MigrationsAssembly(typeof(SwitchPointDbContext).Assembly.FullName));
        return new SwitchPointDbContext(builder.Options, new SystemTenantProvider());
    }
}
