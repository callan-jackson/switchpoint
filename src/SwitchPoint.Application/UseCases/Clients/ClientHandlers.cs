using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Exceptions;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Ports;
using SwitchPoint.Application.Services;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Application.UseCases.Clients;

/// <summary>Shared plumbing for client and scheme handlers.</summary>
public abstract class ClientHandlerBase
{
    protected ClientHandlerBase(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    {
        Clients = clients;
        Schemes = schemes;
        Providers = providers;
        Funds = funds;
        Audit = audit;
        Uow = uow;
        User = user;
        Clock = clock;
    }

    protected IClientRepository Clients { get; }
    protected ISchemeRepository Schemes { get; }
    protected IProviderCatalogue Providers { get; }
    protected IFundCatalogue Funds { get; }
    protected IAuditLog Audit { get; }
    protected IUnitOfWork Uow { get; }
    protected ICurrentUser User { get; }
    protected IClock Clock { get; }

    protected async Task<Client> RequireClientAsync(Guid clientId, CancellationToken ct) =>
        await Clients.GetAsync(User.FirmId, clientId, ct) ?? throw new NotFoundException("Client", clientId);

    protected async Task<SchemeDto> ToDtoAsync(Scheme scheme, IReadOnlyList<Provider>? providers, CancellationToken ct)
    {
        providers ??= await Providers.ListAsync(ct);
        string? providerName = scheme.ProviderId is { } pid ? providers.FirstOrDefault(p => p.Id == pid)?.Name : null;
        decimal? ocf = null;
        if (scheme.Holdings.Count > 0)
        {
            try
            {
                Dictionary<string, decimal> byIsin = (await Funds.ListByIsinsAsync(scheme.Holdings.Where(h => h.Isin is not null).Select(h => h.Isin!), ct)).ToDictionary(f => f.Isin, f => f.Ocf, StringComparer.OrdinalIgnoreCase);
                ocf = Holding.WeightedOcf(scheme.Holdings, h => h.Isin is not null && byIsin.TryGetValue(h.Isin, out decimal o) ? o : null);
            }
            catch (Domain.Common.DomainException)
            {
                ocf = null; // an OCF is unknown for at least one holding
            }
        }

        return scheme.ToDto(providerName, Clock.Today, ocf);
    }

    protected async Task<ClientDetail> ToDetailAsync(Client client, CancellationToken ct)
    {
        IReadOnlyList<Scheme> schemes = await Schemes.ListForClientAsync(User.FirmId, client.Id, ct);
        IReadOnlyList<Provider> providers = await Providers.ListAsync(ct);
        List<SchemeDto> dtos = [];
        foreach (Scheme s in schemes)
        {
            dtos.Add(await ToDtoAsync(s, providers, ct));
        }

        return client.ToDetail(dtos, Clock.Today);
    }

    /// <summary>Audit payload for a client: never the NI number or contact details.</summary>
    protected static object ClientAuditPayload(Client c) => new { c.Id, c.FullName, c.DateOfBirth, c.TargetRetirementAge, c.RiskProfile };
}

public sealed class ListClientsHandler(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ClientHandlerBase(clients, schemes, providers, funds, audit, uow, user, clock)
{
    public async Task<PagedResult<ClientSummary>> HandleAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        Page<Client> result = await Clients.SearchAsync(User.FirmId, search, page, pageSize, ct);
        List<ClientSummary> items = [];
        foreach (Client c in result.Items)
        {
            IReadOnlyList<Scheme> cs = await Schemes.ListForClientAsync(User.FirmId, c.Id, ct);
            items.Add(c.ToSummary(cs.Count, cs.Where(s => s.Type.IsPension()).Sum(s => s.CurrentValue), Clock.Today));
        }

        return new PagedResult<ClientSummary>(items, result.PageNumber, result.PageSize, result.Total);
    }
}

public sealed class GetClientHandler(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ClientHandlerBase(clients, schemes, providers, funds, audit, uow, user, clock)
{
    public async Task<ClientDetail> HandleAsync(Guid clientId, CancellationToken ct) => await ToDetailAsync(await RequireClientAsync(clientId, ct), ct);
}

public sealed class CreateClientHandler(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ClientHandlerBase(clients, schemes, providers, funds, audit, uow, user, clock)
{
    public async Task<ClientDetail> HandleAsync(ClientWrite write, CancellationToken ct)
    {
        Client client = write.ToNewClient(Guid.NewGuid(), User.FirmId, Clock.UtcNow);
        await Clients.AddAsync(client, ct);
        await Audit.AppendAsync(User.FirmId, User.UserId, "Client", client.Id, "Created", ClientAuditPayload(client), ct);
        await Uow.SaveChangesAsync(ct);
        return await ToDetailAsync(client, ct);
    }
}

public sealed class UpdateClientHandler(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ClientHandlerBase(clients, schemes, providers, funds, audit, uow, user, clock)
{
    public async Task<ClientDetail> HandleAsync(Guid clientId, ClientWrite write, CancellationToken ct)
    {
        Client client = await RequireClientAsync(clientId, ct);
        client.ApplyWrite(write, Clock.UtcNow);
        await Audit.AppendAsync(User.FirmId, User.UserId, "Client", client.Id, "Updated", ClientAuditPayload(client), ct);
        await Uow.SaveChangesAsync(ct);
        return await ToDetailAsync(client, ct);
    }
}

public sealed class DeleteClientHandler(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ClientHandlerBase(clients, schemes, providers, funds, audit, uow, user, clock)
{
    public async Task HandleAsync(Guid clientId, CancellationToken ct)
    {
        Client client = await RequireClientAsync(clientId, ct);
        foreach (Scheme s in await Schemes.ListForClientAsync(User.FirmId, clientId, ct))
        {
            await Schemes.RemoveAsync(s, ct);
        }

        await Clients.RemoveAsync(client, ct);
        await Audit.AppendAsync(User.FirmId, User.UserId, "Client", client.Id, "Deleted", new { client.Id }, ct);
        await Uow.SaveChangesAsync(ct);
    }
}

public sealed class ListSchemesHandler(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ClientHandlerBase(clients, schemes, providers, funds, audit, uow, user, clock)
{
    public async Task<IReadOnlyList<SchemeDto>> HandleAsync(Guid clientId, CancellationToken ct)
    {
        await RequireClientAsync(clientId, ct);
        IReadOnlyList<Provider> providers = await Providers.ListAsync(ct);
        List<SchemeDto> list = [];
        foreach (Scheme s in await Schemes.ListForClientAsync(User.FirmId, clientId, ct))
        {
            list.Add(await ToDtoAsync(s, providers, ct));
        }

        return list;
    }
}

public sealed class CreateSchemeHandler(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ClientHandlerBase(clients, schemes, providers, funds, audit, uow, user, clock)
{
    public async Task<SchemeDto> HandleAsync(Guid clientId, SchemeWrite write, CancellationToken ct)
    {
        await RequireClientAsync(clientId, ct);
        Scheme scheme = write.ToNewScheme(Guid.NewGuid(), User.FirmId, clientId, Clock.UtcNow);
        await Schemes.AddAsync(scheme, ct);
        await Audit.AppendAsync(User.FirmId, User.UserId, "Scheme", scheme.Id, "Created", new { scheme.Id, scheme.ClientId, scheme.Type, scheme.ProductName, scheme.CurrentValue, scheme.TransferValue }, ct);
        await Uow.SaveChangesAsync(ct);
        return await ToDtoAsync(scheme, null, ct);
    }
}

public sealed class UpdateSchemeHandler(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ClientHandlerBase(clients, schemes, providers, funds, audit, uow, user, clock)
{
    public async Task<SchemeDto> HandleAsync(Guid clientId, Guid schemeId, SchemeWrite write, CancellationToken ct)
    {
        await RequireClientAsync(clientId, ct);
        Scheme scheme = await Schemes.GetAsync(User.FirmId, schemeId, ct) ?? throw new NotFoundException("Scheme", schemeId);
        if (scheme.ClientId != clientId)
        {
            throw new NotFoundException("Scheme", schemeId);
        }

        if (scheme.Type != write.Type)
        {
            throw new ConflictException("The scheme type cannot be changed; delete and recreate the scheme.");
        }

        scheme.ApplyWrite(write, Clock.UtcNow);
        await Audit.AppendAsync(User.FirmId, User.UserId, "Scheme", scheme.Id, "Updated", new { scheme.Id, scheme.ClientId, scheme.Type, scheme.ProductName, scheme.CurrentValue, scheme.TransferValue }, ct);
        await Uow.SaveChangesAsync(ct);
        return await ToDtoAsync(scheme, null, ct);
    }
}

public sealed class DeleteSchemeHandler(IClientRepository clients, ISchemeRepository schemes, IProviderCatalogue providers, IFundCatalogue funds, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ClientHandlerBase(clients, schemes, providers, funds, audit, uow, user, clock)
{
    public async Task HandleAsync(Guid clientId, Guid schemeId, CancellationToken ct)
    {
        await RequireClientAsync(clientId, ct);
        Scheme scheme = await Schemes.GetAsync(User.FirmId, schemeId, ct) ?? throw new NotFoundException("Scheme", schemeId);
        if (scheme.ClientId != clientId)
        {
            throw new NotFoundException("Scheme", schemeId);
        }

        await Schemes.RemoveAsync(scheme, ct);
        await Audit.AppendAsync(User.FirmId, User.UserId, "Scheme", scheme.Id, "Deleted", new { scheme.Id, scheme.ClientId }, ct);
        await Uow.SaveChangesAsync(ct);
    }
}

public sealed class ListClientAnalysesHandler(IClientRepository clients, IAnalysisRepository analyses, ICurrentUser user)
{
    public async Task<IReadOnlyList<AnalysisSummary>> HandleAsync(Guid clientId, CancellationToken ct)
    {
        _ = await clients.GetAsync(user.FirmId, clientId, ct) ?? throw new NotFoundException("Client", clientId);
        return [.. (await analyses.ListForClientAsync(user.FirmId, clientId, ct)).Select(AnalysisSummaryMapping.ToSummary)];
    }
}

public sealed class ListRecentAnalysesHandler(IAnalysisRepository analyses, ICurrentUser user)
{
    public async Task<IReadOnlyList<AnalysisSummary>> HandleAsync(int take, CancellationToken ct) =>
        [.. (await analyses.ListRecentAsync(user.FirmId, Math.Clamp(take, 1, 100), ct)).Select(AnalysisSummaryMapping.ToSummary)];
}

/// <summary>Summary mapping shared by handlers.</summary>
public static class AnalysisSummaryMapping
{
    public static AnalysisSummary ToSummary(AnalysisBase a)
    {
        ArgumentNullException.ThrowIfNull(a);
        (AnalysisKind kind, string title) = a switch
        {
            PensionSwitchAnalysis p => (AnalysisKind.PensionSwitch, p.Title),
            DbTransferAnalysis => (AnalysisKind.DbTransfer, "Defined benefit transfer"),
            CashflowPlan c => (AnalysisKind.Cashflow, c.Title),
            _ => throw new ArgumentOutOfRangeException(nameof(a), a.GetType().Name, "Unknown analysis type."),
        };
        return new AnalysisSummary(a.Id, kind, title, a.Status, a.Version, a.CalculatedAtUtc, a.UpdatedAtUtc, a.CreatedBy, a.ClientId);
    }
}
