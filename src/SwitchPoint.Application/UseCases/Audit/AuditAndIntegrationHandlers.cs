using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Exceptions;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Ports;
using SwitchPoint.Domain.Audit;
using SwitchPoint.Domain.Clients;
using SwitchPoint.Domain.Market;
using SwitchPoint.Domain.Schemes;

namespace SwitchPoint.Application.UseCases.Audit;

public sealed class QueryAuditHandler(IAuditLog audit, ICurrentUser user)
{
    public async Task<PagedResult<AuditEventDto>> HandleAsync(Guid? entityId, string? entityType, int page, int pageSize, CancellationToken ct)
    {
        Page<AuditEvent> result = await audit.QueryAsync(user.FirmId, entityId, entityType, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), ct);
        return new PagedResult<AuditEventDto>([.. result.Items.Select(e => new AuditEventDto(e.Id, e.Sequence, e.UserId, e.OccurredAtUtc, e.EntityType, e.EntityId, e.Action, e.PayloadJson, e.PreviousHash, e.Hash))], result.PageNumber, result.PageSize, result.Total);
    }
}

public sealed class VerifyAuditChainHandler(IAuditLog audit, ICurrentUser user)
{
    public async Task<ChainVerificationDto> HandleAsync(CancellationToken ct)
    {
        ChainVerification v = await audit.VerifyAsync(user.FirmId, ct);
        return new ChainVerificationDto(v.IsValid, v.FirstBrokenIndex, v.Reason, v.EventsChecked);
    }
}

public sealed class ListIntegrationsHandler(IEnumerable<IBackOfficeConnector> connectors, IFundDataProvider fundData)
{
    public IReadOnlyList<IntegrationStatusDto> Handle() =>
    [
        .. connectors.Select(c => new IntegrationStatusDto(c.Name, c.Mode != IntegrationMode.Disabled, c.Mode.ToString(), c.LastSyncUtc)),
        new IntegrationStatusDto(fundData.Name, fundData.Mode != IntegrationMode.Disabled, fundData.Mode.ToString(), fundData.LastSyncUtc),
    ];
}

/// <summary>Pulls clients and plans from a back-office system and hydrates the client and scheme tables.</summary>
public sealed class ImportFromBackOfficeHandler(IEnumerable<IBackOfficeConnector> connectors, IClientRepository clients, ISchemeRepository schemes, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
{
    public async Task<ImportResult> HandleAsync(string connectorName, ImportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        IBackOfficeConnector connector = connectors.FirstOrDefault(c => string.Equals(c.Name, connectorName, StringComparison.OrdinalIgnoreCase)) ?? throw new NotFoundException("Integration", connectorName);
        if (connector.Mode == IntegrationMode.Disabled)
        {
            throw new ConflictException($"The {connector.Name} integration is not configured.");
        }

        ExternalSource source = Enum.TryParse(connector.Name, true, out ExternalSource s) ? s : ExternalSource.Origo;
        int imported = 0;
        int updated = 0;
        int skipped = 0;
        List<string> messages = [];
        foreach (ImportedClient item in await connector.ImportClientsAsync(request.ExternalClientId, ct))
        {
            try
            {
                Client? existing = await clients.FindByExternalReferenceAsync(user.FirmId, source, item.ExternalId, ct);
                Client client;
                if (existing is null)
                {
                    client = item.Client.ToNewClient(Guid.NewGuid(), user.FirmId, clock.UtcNow);
                    client.LinkExternal(new ExternalReference(source, item.ExternalId), clock.UtcNow);
                    await clients.AddAsync(client, ct);
                    imported++;
                }
                else
                {
                    client = existing;
                    client.ApplyWrite(item.Client, clock.UtcNow);
                    updated++;
                }

                IReadOnlyList<Scheme> current = await schemes.ListForClientAsync(user.FirmId, client.Id, ct);
                foreach (SchemeWrite sw in item.Schemes)
                {
                    Scheme? match = current.FirstOrDefault(x => x.PolicyNumber is not null && x.PolicyNumber == sw.PolicyNumber);
                    if (match is null)
                    {
                        await schemes.AddAsync(sw.ToNewScheme(Guid.NewGuid(), user.FirmId, client.Id, clock.UtcNow), ct);
                    }
                    else
                    {
                        match.ApplyWrite(sw, clock.UtcNow);
                    }
                }

                await audit.AppendAsync(user.FirmId, user.UserId, "Client", client.Id, existing is null ? "Imported" : "ImportUpdated", new { client.Id, Source = source.ToString(), item.ExternalId, Schemes = item.Schemes.Count }, ct);
            }
            catch (Exception ex) when (ex is Domain.Common.DomainException or ArgumentException or ValidationException)
            {
                skipped++;
                messages.Add($"Skipped {item.ExternalId}: {ex.Message}");
            }
        }

        await uow.SaveChangesAsync(ct);
        messages.Insert(0, $"{connector.Name} ({connector.Mode}): {imported} imported, {updated} updated, {skipped} skipped.");
        return new ImportResult(imported, updated, skipped, messages);
    }
}

public sealed class SyncFundsHandler(IFundDataProvider fundData, IAuditLog audit, IUnitOfWork uow, ICurrentUser user)
{
    public async Task<SyncResult> HandleAsync(CancellationToken ct)
    {
        if (fundData.Mode == IntegrationMode.Disabled)
        {
            throw new ConflictException($"The {fundData.Name} feed is not configured.");
        }

        int updated = await fundData.SyncAsync(ct);
        await audit.AppendAsync(user.FirmId, user.UserId, "FundCatalogue", null, "Synced", new { Provider = fundData.Name, Updated = updated }, ct);
        await uow.SaveChangesAsync(ct);
        return new SyncResult(updated, [$"{fundData.Name} ({fundData.Mode}): {updated} funds updated."]);
    }
}
