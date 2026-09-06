using SwitchPoint.Application.Dtos;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Audit;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;
using SwitchPoint.Domain.Tenancy;

namespace SwitchPoint.Application.Ports;

/// <summary>Wall clock abstraction so handlers and engines are testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today => DateOnly.FromDateTime(UtcNow);
}

/// <summary>The authenticated caller. Every handler scopes data access to <see cref="FirmId"/>.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Guid FirmId { get; }
    UserRole Role { get; }
    string DisplayName { get; }
}

/// <summary>Commits a unit of work (one transaction per handler invocation).</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>A page of results.</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int Total);

public interface IFirmRepository
{
    Task<Firm?> GetAsync(Guid firmId, CancellationToken ct = default);
}

public interface IClientRepository
{
    Task<Client?> GetAsync(Guid firmId, Guid clientId, CancellationToken ct = default);
    Task<Page<Client>> SearchAsync(Guid firmId, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<Client?> FindByExternalReferenceAsync(Guid firmId, ExternalSource source, string externalId, CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    Task RemoveAsync(Client client, CancellationToken ct = default);
}

public interface ISchemeRepository
{
    Task<Scheme?> GetAsync(Guid firmId, Guid schemeId, CancellationToken ct = default);
    Task<IReadOnlyList<Scheme>> ListForClientAsync(Guid firmId, Guid clientId, CancellationToken ct = default);
    Task<IReadOnlyList<Scheme>> ListAsync(Guid firmId, IEnumerable<Guid> schemeIds, CancellationToken ct = default);
    Task<int> CountForClientsAsync(Guid firmId, IEnumerable<Guid> clientIds, CancellationToken ct = default);
    Task AddAsync(Scheme scheme, CancellationToken ct = default);
    Task RemoveAsync(Scheme scheme, CancellationToken ct = default);
}

public interface IProviderCatalogue
{
    Task<IReadOnlyList<Provider>> ListAsync(CancellationToken ct = default);
    Task<Provider?> GetAsync(Guid providerId, CancellationToken ct = default);
}

public interface IProductCatalogue
{
    Task<IReadOnlyList<Product>> ListAsync(WrapperTypes? wrapper, string? search, CancellationToken ct = default);
    Task<Product?> GetAsync(Guid productId, CancellationToken ct = default);
}

public interface IFundCatalogue
{
    Task<Page<Fund>> SearchAsync(string? search, string? sector, decimal? maxOcf, int page, int pageSize, CancellationToken ct = default);
    Task<Fund?> GetByIsinAsync(string isin, CancellationToken ct = default);
    Task<Fund?> GetAsync(Guid fundId, CancellationToken ct = default);
    Task<IReadOnlyList<Fund>> ListByIsinsAsync(IEnumerable<string> isins, CancellationToken ct = default);
    Task UpsertAsync(Fund fund, CancellationToken ct = default);
}

public interface IModelPortfolioCatalogue
{
    Task<IReadOnlyList<ModelPortfolio>> ListAsync(CancellationToken ct = default);
    Task<ModelPortfolio?> GetAsync(Guid modelPortfolioId, CancellationToken ct = default);
}

public interface IAssumptionSetRepository
{
    Task<IReadOnlyList<AssumptionSet>> ListAsync(Guid firmId, CancellationToken ct = default);
    Task<AssumptionSet?> GetAsync(Guid firmId, Guid assumptionSetId, CancellationToken ct = default);
    Task<AssumptionSet?> GetFcaStandardAsync(CancellationToken ct = default);
    Task AddAsync(AssumptionSet set, CancellationToken ct = default);
}

public interface IAnalysisRepository
{
    Task<T?> GetAsync<T>(Guid firmId, Guid analysisId, CancellationToken ct = default) where T : AnalysisBase;
    Task<IReadOnlyList<AnalysisBase>> ListForClientAsync(Guid firmId, Guid clientId, CancellationToken ct = default);
    Task<IReadOnlyList<AnalysisBase>> ListRecentAsync(Guid firmId, int take, CancellationToken ct = default);
    Task AddAsync(AnalysisBase analysis, CancellationToken ct = default);
    Task RemoveAsync(AnalysisBase analysis, CancellationToken ct = default);
}

public interface IReportRepository
{
    Task<Report?> GetAsync(Guid firmId, Guid reportId, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> ListForClientAsync(Guid firmId, Guid clientId, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> ListAsync(Guid firmId, int take, CancellationToken ct = default);
    Task AddAsync(Report report, CancellationToken ct = default);
}

/// <summary>Binary storage for rendered reports (local folder, blob storage...).</summary>
public interface IReportStore
{
    Task<string> SaveAsync(Guid firmId, Guid reportId, string fileName, ReadOnlyMemory<byte> content, CancellationToken ct = default);
    Task<ReadOnlyMemory<byte>> LoadAsync(string storagePath, CancellationToken ct = default);
}

/// <summary>Append-only, hash-chained audit log per firm.</summary>
public interface IAuditLog
{
    Task<AuditEvent> AppendAsync(Guid firmId, Guid? userId, string entityType, Guid? entityId, string action, object? payload, CancellationToken ct = default);
    Task<Page<AuditEvent>> QueryAsync(Guid firmId, Guid? entityId, string? entityType, int page, int pageSize, CancellationToken ct = default);
    Task<ChainVerification> VerifyAsync(Guid firmId, CancellationToken ct = default);
}

/// <summary>Credentials check for local login (Identity in Infrastructure).</summary>
public interface IIdentityService
{
    Task<UserDto?> AuthenticateAsync(string email, string password, CancellationToken ct = default);
    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Operating mode of an external integration.</summary>
public enum IntegrationMode
{
    Disabled = 0,
    Sandbox = 1,
    Live = 2,
}

/// <summary>Result of pulling clients/plans from a back-office system.</summary>
public sealed record ImportedClient(ClientWrite Client, string ExternalId, IReadOnlyList<SchemeWrite> Schemes);

/// <summary>Back-office CRM connector (Intelliflo, Xplan, True Potential, Origo Hub).</summary>
public interface IBackOfficeConnector
{
    string Name { get; }
    IntegrationMode Mode { get; }
    DateTime? LastSyncUtc { get; }
    Task<IReadOnlyList<ImportedClient>> ImportClientsAsync(string? externalClientId, CancellationToken ct = default);
}

/// <summary>Fund data feed (Morningstar).</summary>
public interface IFundDataProvider
{
    string Name { get; }
    IntegrationMode Mode { get; }
    DateTime? LastSyncUtc { get; }
    Task<IReadOnlyList<FundDto>> SearchAsync(string query, int take, CancellationToken ct = default);
    Task<FundDto?> GetAsync(string isin, CancellationToken ct = default);
    Task<int> SyncAsync(CancellationToken ct = default);
}

/// <summary>Everything a renderer needs to produce a document.</summary>
public sealed record ReportRequest(
    ReportKind Kind,
    ReportFormat Format,
    Guid AnalysisId,
    int AnalysisVersion,
    string AnalysisResultHash,
    object Analysis,
    ClientDetail Client,
    string FirmName,
    string FirmReferenceNumber,
    AssumptionSetDto AssumptionSet,
    string GeneratedBy,
    DateTime GeneratedAtUtc);

public sealed record ReportDocument(ReadOnlyMemory<byte> Content, string ContentType, string FileName, string TemplateVersion);

public interface IReportRenderer
{
    Task<ReportDocument> RenderAsync(ReportRequest request, CancellationToken ct = default);
}
