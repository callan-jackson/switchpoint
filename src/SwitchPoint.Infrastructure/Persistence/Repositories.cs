using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Ports;
using SwitchPoint.Application.Services;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Audit;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;
using SwitchPoint.Domain.Tenancy;

namespace SwitchPoint.Infrastructure.Persistence;

/// <summary>Wall clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Tenant provider backed by the current user (null when anonymous, e.g. during seeding).</summary>
public sealed class CurrentUserTenantProvider(ICurrentUser user) : ITenantProvider
{
    public Guid? FirmId => user.IsAuthenticated ? user.FirmId : null;
}

public sealed class EfUnitOfWork(SwitchPointDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

public sealed class FirmRepository(SwitchPointDbContext db) : IFirmRepository
{
    public Task<Firm?> GetAsync(Guid firmId, CancellationToken ct = default) => db.Firms.FirstOrDefaultAsync(f => f.Id == firmId, ct);
}

public sealed class ClientRepository(SwitchPointDbContext db) : IClientRepository
{
    public Task<Client?> GetAsync(Guid firmId, Guid clientId, CancellationToken ct = default) =>
        db.Clients.FirstOrDefaultAsync(c => c.FirmId == firmId && c.Id == clientId, ct);

    public async Task<Page<Client>> SearchAsync(Guid firmId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Client> q = db.Clients.Where(c => c.FirmId == firmId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(c => c.FirstName.Contains(term) || c.LastName.Contains(term) || (c.Email != null && c.Email.Contains(term)));
        }

        int total = await q.CountAsync(ct);
        List<Client> items = await q.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new Page<Client>(items, page, pageSize, total);
    }

    public async Task<Client?> FindByExternalReferenceAsync(Guid firmId, ExternalSource source, string externalId, CancellationToken ct = default)
    {
        // ExternalReference is a JSON column; filter in memory over the firm's clients (small per firm).
        List<Client> clients = await db.Clients.Where(c => c.FirmId == firmId).ToListAsync(ct);
        return clients.FirstOrDefault(c => c.ExternalReference.Source == source && string.Equals(c.ExternalReference.ExternalId, externalId, StringComparison.Ordinal));
    }

    public async Task AddAsync(Client client, CancellationToken ct = default) => await db.Clients.AddAsync(client, ct);

    public Task RemoveAsync(Client client, CancellationToken ct = default)
    {
        db.Clients.Remove(client);
        return Task.CompletedTask;
    }
}

public sealed class SchemeRepository(SwitchPointDbContext db) : ISchemeRepository
{
    public Task<Scheme?> GetAsync(Guid firmId, Guid schemeId, CancellationToken ct = default) =>
        db.Schemes.FirstOrDefaultAsync(s => s.FirmId == firmId && s.Id == schemeId, ct);

    public async Task<IReadOnlyList<Scheme>> ListForClientAsync(Guid firmId, Guid clientId, CancellationToken ct = default) =>
        await db.Schemes.Where(s => s.FirmId == firmId && s.ClientId == clientId).OrderBy(s => s.ProductName).ToListAsync(ct);

    public async Task<IReadOnlyList<Scheme>> ListAsync(Guid firmId, IEnumerable<Guid> schemeIds, CancellationToken ct = default)
    {
        List<Guid> ids = [.. schemeIds];
        return await db.Schemes.Where(s => s.FirmId == firmId && ids.Contains(s.Id)).ToListAsync(ct);
    }

    public Task<int> CountForClientsAsync(Guid firmId, IEnumerable<Guid> clientIds, CancellationToken ct = default)
    {
        List<Guid> ids = [.. clientIds];
        return db.Schemes.CountAsync(s => s.FirmId == firmId && ids.Contains(s.ClientId), ct);
    }

    public async Task AddAsync(Scheme scheme, CancellationToken ct = default) => await db.Schemes.AddAsync(scheme, ct);

    public Task RemoveAsync(Scheme scheme, CancellationToken ct = default)
    {
        db.Schemes.Remove(scheme);
        return Task.CompletedTask;
    }
}

public sealed class ProviderCatalogue(SwitchPointDbContext db) : IProviderCatalogue
{
    public async Task<IReadOnlyList<Provider>> ListAsync(CancellationToken ct = default) => await db.Providers.OrderBy(p => p.Name).ToListAsync(ct);

    public Task<Provider?> GetAsync(Guid providerId, CancellationToken ct = default) => db.Providers.FirstOrDefaultAsync(p => p.Id == providerId, ct);
}

public sealed class ProductCatalogue(SwitchPointDbContext db) : IProductCatalogue
{
    public async Task<IReadOnlyList<Product>> ListAsync(WrapperTypes? wrapper, string? search, CancellationToken ct = default)
    {
        IQueryable<Product> q = db.Products;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(p => p.Name.Contains(term));
        }

        List<Product> items = await q.OrderBy(p => p.Name).ToListAsync(ct);
        return wrapper is { } w ? [.. items.Where(p => p.Supports(w))] : items;
    }

    public Task<Product?> GetAsync(Guid productId, CancellationToken ct = default) => db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
}

public sealed class FundCatalogue(SwitchPointDbContext db) : IFundCatalogue
{
    public async Task<Page<Fund>> SearchAsync(string? search, string? sector, decimal? maxOcf, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Fund> q = db.Funds;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            q = q.Where(f => f.Name.Contains(term) || f.Isin.Contains(term) || f.ManagerName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(sector))
        {
            q = q.Where(f => f.IaSector == sector);
        }

        if (maxOcf is { } max)
        {
            q = q.Where(f => f.Ocf <= max);
        }

        int total = await q.CountAsync(ct);
        List<Fund> items = await q.OrderBy(f => f.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new Page<Fund>(items, page, pageSize, total);
    }

    public Task<Fund?> GetByIsinAsync(string isin, CancellationToken ct = default)
    {
        string upper = isin.ToUpperInvariant();
        return db.Funds.FirstOrDefaultAsync(f => f.Isin == upper, ct);
    }

    public Task<Fund?> GetAsync(Guid fundId, CancellationToken ct = default) => db.Funds.FirstOrDefaultAsync(f => f.Id == fundId, ct);

    public async Task<IReadOnlyList<Fund>> ListByIsinsAsync(IEnumerable<string> isins, CancellationToken ct = default)
    {
        List<string> list = [.. isins.Select(i => i.ToUpperInvariant()).Distinct()];
        return list.Count == 0 ? [] : await db.Funds.Where(f => list.Contains(f.Isin)).ToListAsync(ct);
    }

    public async Task UpsertAsync(Fund fund, CancellationToken ct = default)
    {
        Fund? existing = await db.Funds.FirstOrDefaultAsync(f => f.Isin == fund.Isin, ct);
        if (existing is null)
        {
            await db.Funds.AddAsync(fund, ct);
            return;
        }

        existing.UpdateProfile(fund.Name, fund.ManagerName, fund.ShareClass, fund.Sedol, fund.IaSector, fund.MorningstarCategory, fund.Srri, fund.AssetAllocation, fund.FactsheetUrl, DateTime.UtcNow);
        existing.UpdateCharges(fund.Ocf, fund.TransactionCosts, fund.AsAt ?? DateOnly.FromDateTime(DateTime.UtcNow), fund.SourceUrl, DateTime.UtcNow);
        existing.UpdateStatistics(fund.Statistics, fund.Price, fund.PriceDate, DateTime.UtcNow);
    }
}

public sealed class ModelPortfolioCatalogue(SwitchPointDbContext db) : IModelPortfolioCatalogue
{
    public async Task<IReadOnlyList<ModelPortfolio>> ListAsync(CancellationToken ct = default) => await db.ModelPortfolios.OrderBy(m => m.Name).ToListAsync(ct);

    public Task<ModelPortfolio?> GetAsync(Guid modelPortfolioId, CancellationToken ct = default) => db.ModelPortfolios.FirstOrDefaultAsync(m => m.Id == modelPortfolioId, ct);
}

public sealed class AssumptionSetRepository(SwitchPointDbContext db) : IAssumptionSetRepository
{
    public async Task<IReadOnlyList<AssumptionSet>> ListAsync(Guid firmId, CancellationToken ct = default) =>
        await db.AssumptionSets.Where(a => a.FirmId == null || a.FirmId == firmId).OrderByDescending(a => a.IsFcaStandard).ThenBy(a => a.Name).ToListAsync(ct);

    public Task<AssumptionSet?> GetAsync(Guid firmId, Guid assumptionSetId, CancellationToken ct = default) =>
        db.AssumptionSets.FirstOrDefaultAsync(a => a.Id == assumptionSetId && (a.FirmId == null || a.FirmId == firmId), ct);

    public Task<AssumptionSet?> GetFcaStandardAsync(CancellationToken ct = default) =>
        db.AssumptionSets.Where(a => a.IsFcaStandard).OrderByDescending(a => a.CreatedAtUtc).FirstOrDefaultAsync(ct);

    public async Task AddAsync(AssumptionSet set, CancellationToken ct = default) => await db.AssumptionSets.AddAsync(set, ct);
}

public sealed class AnalysisRepository(SwitchPointDbContext db) : IAnalysisRepository
{
    public async Task<T?> GetAsync<T>(Guid firmId, Guid analysisId, CancellationToken ct = default) where T : AnalysisBase =>
        await db.Analyses.OfType<T>().FirstOrDefaultAsync(a => a.FirmId == firmId && a.Id == analysisId, ct);

    public async Task<IReadOnlyList<AnalysisBase>> ListForClientAsync(Guid firmId, Guid clientId, CancellationToken ct = default) =>
        await db.Analyses.Where(a => a.FirmId == firmId && a.ClientId == clientId).OrderByDescending(a => a.UpdatedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<AnalysisBase>> ListRecentAsync(Guid firmId, int take, CancellationToken ct = default) =>
        await db.Analyses.Where(a => a.FirmId == firmId).OrderByDescending(a => a.UpdatedAtUtc).Take(take).ToListAsync(ct);

    public async Task AddAsync(AnalysisBase analysis, CancellationToken ct = default) => await db.Analyses.AddAsync(analysis, ct);

    public Task RemoveAsync(AnalysisBase analysis, CancellationToken ct = default)
    {
        db.Analyses.Remove(analysis);
        return Task.CompletedTask;
    }
}

public sealed class ReportRepository(SwitchPointDbContext db) : IReportRepository
{
    public Task<Report?> GetAsync(Guid firmId, Guid reportId, CancellationToken ct = default) => db.Reports.FirstOrDefaultAsync(r => r.FirmId == firmId && r.Id == reportId, ct);

    public async Task<IReadOnlyList<Report>> ListForClientAsync(Guid firmId, Guid clientId, CancellationToken ct = default) =>
        await db.Reports.Where(r => r.FirmId == firmId && r.ClientId == clientId).OrderByDescending(r => r.GeneratedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<Report>> ListAsync(Guid firmId, int take, CancellationToken ct = default) =>
        await db.Reports.Where(r => r.FirmId == firmId).OrderByDescending(r => r.GeneratedAtUtc).Take(take).ToListAsync(ct);

    public async Task AddAsync(Report report, CancellationToken ct = default) => await db.Reports.AddAsync(report, ct);
}

/// <summary>Stores report files under a configurable root: {root}/{firmId}/{reportId}/{fileName}.</summary>
public sealed class FileReportStore(string rootPath) : IReportStore
{
    public string RootPath { get; } = Path.GetFullPath(rootPath);

    public async Task<string> SaveAsync(Guid firmId, Guid reportId, string fileName, ReadOnlyMemory<byte> content, CancellationToken ct = default)
    {
        string safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || safeName != fileName)
        {
            throw new ArgumentException("File name must be a plain file name.", nameof(fileName));
        }

        string dir = Path.Combine(RootPath, firmId.ToString("N"), reportId.ToString("N"));
        Directory.CreateDirectory(dir);
        string full = Path.Combine(dir, safeName);
        await File.WriteAllBytesAsync(full, content.ToArray(), ct);
        return Path.GetRelativePath(RootPath, full).Replace('\\', '/');
    }

    public async Task<ReadOnlyMemory<byte>> LoadAsync(string storagePath, CancellationToken ct = default)
    {
        string full = Path.GetFullPath(Path.Combine(RootPath, storagePath));
        if (!full.StartsWith(RootPath, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Storage path escapes the report store.");
        }

        return await File.ReadAllBytesAsync(full, ct);
    }
}

/// <summary>Append-only hash-chained audit log per firm (ADR-0005).</summary>
public sealed class HashChainAuditLog(SwitchPointDbContext db, IClock clock) : IAuditLog
{
    /// <summary>
    /// Reads the firm's last sequence and stages the next event. There is deliberately no in-process
    /// lock here: the read and the write are separated by the caller's SaveChangesAsync, so a lock
    /// taken and released inside this method would not span the critical section, and would protect
    /// nothing at all once the app runs on more than one instance.
    ///
    /// Correctness comes from the database instead. The unique index on (FirmId, Sequence) means two
    /// racing requests cannot both commit sequence n: the loser's SaveChangesAsync fails, and the
    /// unit of work is retried rather than forking the chain. That is the only guarantee that holds
    /// across instances, which is what a tamper-evident log needs.
    /// </summary>
    public async Task<AuditEvent> AppendAsync(Guid firmId, Guid? userId, string entityType, Guid? entityId, string action, object? payload, CancellationToken ct = default)
    {
        string json = payload is null ? "{}" : JsonDefaults.Serialize(payload);

        // Include events added in this unit of work but not yet saved, so several appends per request chain correctly.
        AuditEvent? pendingLast = db.ChangeTracker.Entries<AuditEvent>().Where(e => e.State == EntityState.Added && e.Entity.FirmId == firmId).Select(e => e.Entity).OrderByDescending(e => e.Sequence).FirstOrDefault();
        AuditEvent? storedLast = await db.AuditEvents.IgnoreQueryFilters().Where(e => e.FirmId == firmId).OrderByDescending(e => e.Sequence).FirstOrDefaultAsync(ct);
        AuditEvent? last = pendingLast is not null && (storedLast is null || pendingLast.Sequence > storedLast.Sequence) ? pendingLast : storedLast;
        long sequence = last is null ? 0 : last.Sequence + 1;
        string previous = last?.Hash ?? AuditEvent.GenesisHash;
        AuditEvent e = new(Guid.NewGuid(), firmId, sequence, userId, clock.UtcNow, entityType, entityId, action, json, previous);
        await db.AuditEvents.AddAsync(e, ct);
        return e;
    }

    public async Task<Page<AuditEvent>> QueryAsync(Guid firmId, Guid? entityId, string? entityType, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<AuditEvent> q = db.AuditEvents.IgnoreQueryFilters().Where(e => e.FirmId == firmId);
        if (entityId is { } id)
        {
            q = q.Where(e => e.EntityId == id);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            q = q.Where(e => e.EntityType == entityType);
        }

        int total = await q.CountAsync(ct);
        List<AuditEvent> items = await q.OrderByDescending(e => e.Sequence).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new Page<AuditEvent>(items, page, pageSize, total);
    }

    public async Task<ChainVerification> VerifyAsync(Guid firmId, CancellationToken ct = default)
    {
        List<AuditEvent> events = await db.AuditEvents.IgnoreQueryFilters().AsNoTracking().Where(e => e.FirmId == firmId).OrderBy(e => e.Sequence).ToListAsync(ct);
        return HashChainVerifier.Verify(events);
    }
}

/// <summary>Local credential check backed by ASP.NET Core Identity.</summary>
public sealed class IdentityService(UserManager<ApplicationUser> users, SwitchPointDbContext db) : IIdentityService
{
    public async Task<UserDto?> AuthenticateAsync(string email, string password, CancellationToken ct = default)
    {
        ApplicationUser? user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        // CheckPasswordAsync on its own only verifies the hash: it neither reads the lockout flag
        // nor records a failure, which would leave MaxFailedAccessAttempts as decoration and the
        // only authentication endpoint open to unlimited guessing. Drive the lockout explicitly.
        if (await users.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return null;
        }

        await users.ResetAccessFailedCountAsync(user);
        return await ToDtoAsync(user, ct);
    }

    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        ApplicationUser? user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : await ToDtoAsync(user, ct);
    }

    private async Task<UserDto> ToDtoAsync(ApplicationUser user, CancellationToken ct)
    {
        Firm? firm = await db.Firms.FirstOrDefaultAsync(f => f.Id == user.FirmId, ct);
        return new UserDto(user.Id, user.DisplayName, user.Email ?? string.Empty, user.Role, user.FirmId, firm?.Name ?? "Unknown firm");
    }
}
