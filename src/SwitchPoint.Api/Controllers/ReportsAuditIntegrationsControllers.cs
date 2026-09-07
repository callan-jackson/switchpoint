using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchPoint.Api.Auth;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.UseCases.Audit;
using SwitchPoint.Application.UseCases.Reports;

namespace SwitchPoint.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController(ReportHandlers reports) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyList<ReportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> List([FromQuery] int take = 50, CancellationToken ct = default) => Ok(await reports.ListRecentAsync(take, ct));

    /// <summary>Renders a report from a calculated analysis and locks the analysis.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Adviser)]
    [Produces("application/json")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReportDto>> Create([FromBody] CreateReportRequest request, CancellationToken ct)
    {
        ReportDto created = await reports.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportDto>> Get(Guid id, CancellationToken ct) => Ok(await reports.GetAsync(id, ct));

    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        ReportFile file = await reports.DownloadAsync(id, ct);
        return File(file.Content.ToArray(), file.ContentType, file.FileName);
    }
}

[ApiController]
[Route("api/v1/audit")]
[Produces("application/json")]
public sealed class AuditController(QueryAuditHandler query, VerifyAuditChainHandler verify) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditEventDto>>> List([FromQuery] Guid? entityId, [FromQuery] string? entityType, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await query.HandleAsync(entityId, entityType, page, pageSize, ct));

    /// <summary>Re-walks the firm's hash chain and reports the first broken link, if any.</summary>
    [HttpGet("verify")]
    [Authorize(Policy = Policies.Compliance)]
    [ProducesResponseType<ChainVerificationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChainVerificationDto>> Verify(CancellationToken ct) => Ok(await verify.HandleAsync(ct));
}

[ApiController]
[Route("api/v1/integrations")]
[Produces("application/json")]
public sealed class IntegrationsController(ListIntegrationsHandler list, ImportFromBackOfficeHandler import, SyncFundsHandler sync) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<IntegrationStatusDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<IntegrationStatusDto>> List() => Ok(list.Handle());

    [HttpPost("{connector}/import")]
    [Authorize(Policy = Policies.Adviser)]
    [ProducesResponseType<ImportResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportResult>> Import(string connector, [FromBody] ImportRequest? request, CancellationToken ct) =>
        Ok(await import.HandleAsync(connector, request ?? new ImportRequest(null), ct));

    [HttpPost("morningstar/sync")]
    [Authorize(Policy = Policies.FirmAdmin)]
    [ProducesResponseType<SyncResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncResult>> Sync(CancellationToken ct) => Ok(await sync.HandleAsync(ct));
}
